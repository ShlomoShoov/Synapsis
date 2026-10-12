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

namespace Synapsis.Elasticsearch;

/// <summary>Consumes document IDs, loads and converts records, then calls the Elasticsearch repository.</summary>
public sealed class ElasticsearchOrchestrator : BackgroundService
{
    private readonly KafkaConsumerContext _kafkaConsumer;
    private readonly MongoRecordRepository _mongoRecordRepository;
    private readonly ElasticsearchRepository _elasticsearchRepository;
    private readonly ILogger<ElasticsearchOrchestrator> _logger;

    /// <summary>Creates the Elasticsearch processing flow.</summary>
    public ElasticsearchOrchestrator(
        KafkaConsumerContext kafkaConsumer,
        MongoRecordRepository mongoRecordRepository,
        ElasticsearchRepository elasticsearchRepository,
        ILogger<ElasticsearchOrchestrator> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _mongoRecordRepository = mongoRecordRepository;
        _elasticsearchRepository = elasticsearchRepository;
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
            return;
        }

        // 2. Load the stored MongoDB document.
        StoredRecord? storedRecord = await _mongoRecordRepository.FindAsync(documentKey, stoppingToken);
        if (storedRecord == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} was not found.", documentKey);
            _kafkaConsumer.Commit(message);
            return;
        }

        // 3. Convert the stored record into its existing SIL record type.
        JsonElement json = RecordReader.ToJson(storedRecord.Record);
        (SilRecord? record, string? error) = RecordReader.Read(json);
        if (record == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} could not be converted: {Reason}", documentKey, error);
            _kafkaConsumer.Commit(message);
            return;
        }

        // 4. Pass the typed record to the destination repository.
        await _elasticsearchRepository.StoreAsync(record, stoppingToken);

        // 5. Commit only after successful processing.
        _kafkaConsumer.Commit(message);
    }
}
