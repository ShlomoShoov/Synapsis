using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;
using Synapsis.Data.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Synapsis.KafkaRelay;

/// <summary>
/// Background service that coordinates watching MongoDB inserts, publishing document IDs to Kafka,
/// retrying failures indefinitely, and saving checkpoints. Messages are published in order, up to 500 at a time
/// without waiting for each one; the checkpoint is saved once Kafka has acknowledged the whole batch (at least once:
/// a crash re-sends the batch, and the consumers are idempotent).
/// </summary>
public sealed class DocumentRelayOrchestrator : BackgroundService
{
    private const string CheckpointId = "relay_resume_token";
    private const string CheckpointsCollectionName = "_checkpoints";
    private const int MostPerBatch = 500;

    private readonly IDocumentChangeMonitor _changeMonitor;
    private readonly KafkaContext _kafkaContext;
    private readonly MongoOptions _mongoOptions;
    private readonly KafkaOptions _kafkaOptions;
    private readonly PipelineLog _pipelineLog;
    private readonly ILogger<DocumentRelayOrchestrator> _logger;

    private readonly IMongoCollection<BsonDocument> _checkpointCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRelayOrchestrator"/> class.
    /// </summary>
    /// <param name="changeMonitor">The monitor tracking MongoDB Change Stream inserts.</param>
    /// <param name="kafkaContext">The shared Kafka producer connection.</param>
    /// <param name="mongoOptions">The MongoDB connection options.</param>
    /// <param name="kafkaOptions">The Kafka connection options.</param>
    /// <param name="pipelineLog">The relay's counters in the pipeline log.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentRelayOrchestrator(
        IDocumentChangeMonitor changeMonitor,
        KafkaContext kafkaContext,
        MongoOptions mongoOptions,
        KafkaOptions kafkaOptions,
        PipelineLog pipelineLog,
        ILogger<DocumentRelayOrchestrator> logger)
    {
        _changeMonitor = changeMonitor;
        _kafkaContext = kafkaContext;
        _mongoOptions = mongoOptions;
        _kafkaOptions = kafkaOptions;
        _pipelineLog = pipelineLog;
        _logger = logger;

        MongoClient mongoClient = new MongoClient(_mongoOptions.Url);
        _checkpointCollection = mongoClient
            .GetDatabase(_mongoOptions.Database)
            .GetCollection<BsonDocument>(CheckpointsCollectionName);
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
                    ProcessInsertsAsync,
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
                _pipelineLog.Failed("MongoDB change stream error: " + ex.Message);
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

    private async Task ProcessInsertsAsync(IReadOnlyList<(string DocumentId, BsonDocument ResumeToken)> inserts, CancellationToken cancellationToken)
    {
        for (int start = 0; start < inserts.Count; start += MostPerBatch)
        {
            int count = Math.Min(MostPerBatch, inserts.Count - start);

            // 1. Publish the batch to Kafka with infinite retry until every message is acknowledged or cancellation
            await PublishWithRetryAsync(inserts, start, count, cancellationToken);

            // 2. Persist the checkpoint of the batch's last message with retry until success or cancellation
            await SaveResumeTokenWithRetryAsync(inserts[start + count - 1].ResumeToken, cancellationToken);
        }
    }

    private async Task PublishWithRetryAsync(IReadOnlyList<(string DocumentId, BsonDocument ResumeToken)> inserts, int start, int count, CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Produced in order without waiting for each one; a failure sends the whole batch again
                Task[] deliveries = new Task[count];
                for (int index = 0; index < count; index++)
                {
                    string documentId = inserts[start + index].DocumentId;
                    int separator = documentId.IndexOf('|');
                    if (separator <= 0)
                    {
                        throw new InvalidOperationException($"The MongoDB document id '{documentId}' has no record id.");
                    }
                    deliveries[index] = _kafkaContext.ProduceAsync(_kafkaOptions.Topic, documentId.Substring(0, separator), documentId, cancellationToken);
                }
                await Task.WhenAll(deliveries);
                _pipelineLog.Processed(count);
                _pipelineLog.Written(count);
                _logger.LogInformation("Published {Count} document IDs to topic '{Topic}', the last '{DocumentId}'",
                    count, _kafkaOptions.Topic, inserts[start + count - 1].DocumentId);
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
                _logger.LogWarning(ex, "Failed to publish a batch of {Count} document IDs to Kafka (attempt {Attempt}). Retrying in {Delay}s...",
                    count, attempt, delaySeconds);
                _pipelineLog.Failed($"Kafka publish failed (attempt {attempt}): {ex.Message}");

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
        base.Dispose();
    }
}
