using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;

namespace Synapsis.Neo4j;

/// <summary>Consumes document IDs, loads and converts records, then calls the Neo4j repository.</summary>
public sealed class Neo4jOrchestrator : BackgroundService
{
    private readonly KafkaConsumerContext _kafkaConsumer;
    private readonly MongoRecordRepository _mongoRecordRepository;
    private readonly Neo4jRepository _neo4jRepository;
    private readonly PipelineLog _pipelineLog;
    private readonly ILogger<Neo4jOrchestrator> _logger;

    /// <summary>Creates the Neo4j processing flow.</summary>
    public Neo4jOrchestrator(
        KafkaConsumerContext kafkaConsumer,
        MongoRecordRepository mongoRecordRepository,
        Neo4jRepository neo4jRepository,
        PipelineLog pipelineLog,
        ILogger<Neo4jOrchestrator> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _mongoRecordRepository = mongoRecordRepository;
        _neo4jRepository = neo4jRepository;
        _pipelineLog = pipelineLog;
        _pipelineLog.Lag = kafkaConsumer.Lag;
        _logger = logger;
    }

    /// <summary>Runs the manual-commit Kafka processing loop.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<Ignore, string> message;
            try
            {
                message = _kafkaConsumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Kafka consumption failed. Retrying shortly.");
                _pipelineLog.Failed("Kafka consumption failed: " + exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            await ProcessMessageWithRetryAsync(message, stoppingToken);
        }
    }

    /// <summary>Processes one message until it is committed or shutdown is requested.</summary>
    private async Task ProcessMessageWithRetryAsync(ConsumeResult<Ignore, string> message, CancellationToken stoppingToken)
    {
        int attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                attempt++;
                int delaySeconds = Math.Min((int)Math.Pow(2, Math.Min(attempt, 5)), 30);
                _logger.LogWarning(exception, "Record processing failed at offset {Offset}. Retrying in {Delay} seconds.", message.Offset, delaySeconds);
                _pipelineLog.Failed($"Offset {message.Offset.Value} failed (attempt {attempt}): {exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
        }
    }

    /// <summary>Runs the five agreed processing steps for one Kafka message.</summary>
    private async Task ProcessMessageAsync(ConsumeResult<Ignore, string> message, CancellationToken stoppingToken)
    {
        // 1. Read and validate the document ID.
        string documentKey = message.Message.Value;
        if (string.IsNullOrWhiteSpace(documentKey))
        {
            _logger.LogError("Kafka message at offset {Offset} has no document ID.", message.Offset);
            _kafkaConsumer.Commit(message);
            _pipelineLog.Processed();
            _pipelineLog.Failed($"Kafka message at offset {message.Offset.Value} has no document ID.");
            return;
        }

        // 2. Load the stored MongoDB document.
        StoredRecord? storedRecord = await _mongoRecordRepository.FindAsync(documentKey, stoppingToken);
        if (storedRecord == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} was not found.", documentKey);
            _kafkaConsumer.Commit(message);
            _pipelineLog.Processed();
            _pipelineLog.Failed($"MongoDB document {documentKey} was not found.");
            return;
        }

        // 3. Convert the stored record into its existing SIL record type.
        JsonElement json = RecordReader.ToJson(storedRecord.Record);
        (SilRecord? record, string? error) = RecordReader.Read(json);
        if (record == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} could not be converted: {Reason}", documentKey, error);
            _kafkaConsumer.Commit(message);
            _pipelineLog.Processed();
            _pipelineLog.Failed($"MongoDB document {documentKey} could not be converted: {error}");
            return;
        }

        // 4. Pass the typed record to the destination repository; an event that is not a node (no participant, or a
        // type kept out, such as EPSS and NVD) writes nothing, so it opens no transaction.
        bool skipped = record is EventRecord eventRecord && GraphRecordRules.ParticipantsOfConnectingEvent(eventRecord).Count == 0;
        if (!skipped)
        {
            await _neo4jRepository.StoreAsync(record, stoppingToken);
        }

        // 5. Commit only after successful processing.
        _kafkaConsumer.Commit(message);
        _pipelineLog.Processed();
        if (skipped)
        {
            _pipelineLog.Skipped();
        }
        else
        {
            _pipelineLog.Written();
        }
    }
}
