using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Synapsis.KafkaRelay;

/// <summary>
/// Background service that coordinates watching MongoDB inserts, publishing document IDs to Kafka,
/// retrying failures indefinitely, and saving checkpoints.
/// </summary>
public sealed class DocumentRelayOrchestrator : BackgroundService
{
    private const string CheckpointId = "relay_resume_token";
    private const string CheckpointsCollectionName = "_checkpoints";

    private readonly IDocumentChangeMonitor _changeMonitor;
    private readonly IMongoContext _mongoContext;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<DocumentRelayOrchestrator> _logger;

    private readonly IProducer<Null, string> _producer;
    private readonly IMongoCollection<BsonDocument> _checkpointCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRelayOrchestrator"/> class.
    /// </summary>
    /// <param name="changeMonitor">The monitor tracking MongoDB Change Stream inserts.</param>
    /// <param name="mongoContext">The MongoDB data access context.</param>
    /// <param name="kafkaOptions">The Kafka connection options.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentRelayOrchestrator(
        IDocumentChangeMonitor changeMonitor,
        IMongoContext mongoContext,
        KafkaOptions kafkaOptions,
        ILogger<DocumentRelayOrchestrator> logger)
    {
        _changeMonitor = changeMonitor;
        _mongoContext = mongoContext;
        _kafkaOptions = kafkaOptions;
        _logger = logger;

        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = _kafkaOptions.Bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        _checkpointCollection = _mongoContext.Collection<BsonDocument>(CheckpointsCollectionName);
    }

    /// <summary>
    /// Executes the background processing loop.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting DocumentRelayOrchestrator for topic '{Topic}'...", _kafkaOptions.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                BsonDocument? resumeToken = await LoadResumeTokenAsync(stoppingToken);

                await _changeMonitor.WatchInsertsAsync(
                    resumeToken,
                    ProcessInsertEventAsync,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DocumentRelayOrchestrator stopping due to cancellation.");
                break;
            }
            catch (MongoCommandException ex) when (ex.CodeName == "ChangeStreamHistoryLost" || ex.Code == 286)
            {
                _logger.LogCritical(ex, "Unrecoverable condition: MongoDB oplog history for resume token has expired. Manual intervention required.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MongoDB change stream encountered an error or disconnection. Reconnecting in 5 seconds...");
                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessInsertEventAsync(string documentId, BsonDocument resumeToken, CancellationToken cancellationToken)
    {
        // 1. Publish to Kafka with infinite retry until success or cancellation
        await PublishWithRetryAsync(documentId, cancellationToken);

        // 2. Persist checkpoint with retry until success or cancellation
        await SaveResumeTokenWithRetryAsync(resumeToken, cancellationToken);
    }

    private async Task PublishWithRetryAsync(string documentId, CancellationToken cancellationToken)
    {
        int attempt = 0;
        Message<Null, string> message = new Message<Null, string> { Value = documentId };

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DeliveryResult<Null, string> deliveryResult = await _producer.ProduceAsync(_kafkaOptions.Topic, message, cancellationToken);
                _logger.LogInformation("Published document ID '{DocumentId}' to topic '{Topic}', partition {Partition}, offset {Offset}",
                    documentId, _kafkaOptions.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                int delaySeconds = Math.Min((int)Math.Pow(2, Math.Min(attempt, 5)), 30);
                _logger.LogWarning(ex, "Failed to publish document ID '{DocumentId}' to Kafka (attempt {Attempt}). Retrying in {Delay}s...",
                    documentId, attempt, delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
    }

    private async Task<BsonDocument?> LoadResumeTokenAsync(CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", CheckpointId);
        BsonDocument? checkpointDoc = await _checkpointCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (checkpointDoc != null && checkpointDoc.Contains("token") && checkpointDoc["token"].IsBsonDocument)
        {
            return checkpointDoc["token"].AsBsonDocument;
        }

        return null;
    }

    private async Task SaveResumeTokenWithRetryAsync(BsonDocument resumeToken, CancellationToken cancellationToken)
    {
        int attempt = 0;
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", CheckpointId);
        BsonDocument document = new BsonDocument
        {
            { "_id", CheckpointId },
            { "token", resumeToken },
            { "updated_at", DateTime.UtcNow }
        };

        ReplaceOptions options = new ReplaceOptions { IsUpsert = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _checkpointCollection.ReplaceOneAsync(filter, document, options, cancellationToken);
                _logger.LogDebug("Successfully persisted checkpoint resume token.");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                int delaySeconds = Math.Min((int)Math.Pow(2, Math.Min(attempt, 5)), 30);
                _logger.LogWarning(ex, "Failed to persist checkpoint to MongoDB (attempt {Attempt}). Retrying in {Delay}s...",
                    attempt, delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Flushes and disposes the Kafka producer when shutting down.
    /// </summary>
    public override void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing Kafka producer.");
        }

        base.Dispose();
    }
}
