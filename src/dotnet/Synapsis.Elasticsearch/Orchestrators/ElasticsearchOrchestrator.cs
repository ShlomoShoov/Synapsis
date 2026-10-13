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

/// <summary>
/// The indexer: consumes document keys from Kafka, loads each stored record from MongoDB and writes its search document
/// (with the vector of its text) to Elasticsearch. One message at a time, committed after it is handled.
/// </summary>
public sealed class ElasticsearchOrchestrator : BackgroundService
{
    private readonly KafkaConsumerContext _kafkaConsumer;
    private readonly MongoRecordRepository _mongoRecordRepository;
    private readonly ElasticsearchRepository _elasticsearchRepository;
    private readonly EmbedderClient _embedderClient;
    private readonly PipelineLog _pipelineLog;
    private readonly ILogger<ElasticsearchOrchestrator> _logger;

    /// <summary>Creates the indexer over Kafka, MongoDB, Elasticsearch and the embedder.</summary>
    public ElasticsearchOrchestrator(
        KafkaConsumerContext kafkaConsumer,
        MongoRecordRepository mongoRecordRepository,
        ElasticsearchRepository elasticsearchRepository,
        EmbedderClient embedderClient,
        PipelineLog pipelineLog,
        ILogger<ElasticsearchOrchestrator> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _mongoRecordRepository = mongoRecordRepository;
        _elasticsearchRepository = elasticsearchRepository;
        _embedderClient = embedderClient;
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

    /// <summary>
    /// The agreed steps for one Kafka message. Every path ends with a commit, except a failure of a store or of the
    /// embedder, which throws so the same message is tried again.
    /// </summary>
    private async Task ProcessMessageAsync(ConsumeResult<Ignore, string> message, CancellationToken stoppingToken)
    {
        // 1. Load the stored MongoDB document named by the message.
        StoredRecord? storedRecord = await LoadDocumentAsync(message, stoppingToken);

        // 2. A relation is not indexed, and a state already in Elasticsearch is not written again (no second vector).
        bool written = false;
        if (storedRecord != null && await IsToIndexAsync(storedRecord, stoppingToken))
        {
            // 3. Read the record and build its search document.
            SearchDocument? document = BuildDocument(storedRecord);

            // 4. Add the vector of its text, then write it.
            if (document != null)
            {
                await AddVectorAsync(document, storedRecord.Key, stoppingToken);
                written = await WriteAsync(document, storedRecord.Key, stoppingToken);
            }
        }

        // 5. Commit: the message is handled.
        _kafkaConsumer.Commit(message);
        _pipelineLog.Processed();
        if (written)
        {
            _pipelineLog.Written();
        }
        else
        {
            _pipelineLog.Skipped();
        }
    }

    /// <summary>The stored document the message names, or null (logged) when the message is empty or the document is missing.</summary>
    private async Task<StoredRecord?> LoadDocumentAsync(ConsumeResult<Ignore, string> message, CancellationToken stoppingToken)
    {
        string documentKey = message.Message.Value;
        if (string.IsNullOrWhiteSpace(documentKey))
        {
            _logger.LogError("Kafka message at offset {Offset} has no document ID.", message.Offset);
            return null;
        }
        StoredRecord? storedRecord = await _mongoRecordRepository.FindAsync(documentKey, stoppingToken);
        if (storedRecord == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} was not found.", documentKey);
        }
        return storedRecord;
    }

    /// <summary>False for a relation, and for a state that is already in the index.</summary>
    private async Task<bool> IsToIndexAsync(StoredRecord storedRecord, CancellationToken stoppingToken)
    {
        if (storedRecord.Kind == "relation")
        {
            return false;
        }
        bool exists = await _elasticsearchRepository.ExistsAsync(storedRecord.Key, stoppingToken);
        if (exists)
        {
            _logger.LogDebug("{DocumentKey} is already indexed.", storedRecord.Key);
        }
        return !exists;
    }

    /// <summary>The record read from the stored document and mapped; null (logged) when it cannot be read.</summary>
    private SearchDocument? BuildDocument(StoredRecord storedRecord)
    {
        JsonElement json = RecordReader.ToJson(storedRecord.Record);
        (SilRecord? record, string? error) = RecordReader.Read(json);
        if (record == null)
        {
            _logger.LogError("MongoDB document {DocumentKey} could not be read: {Reason}", storedRecord.Key, error);
            return null;
        }
        return SearchDocumentMapper.Map(storedRecord, record);
    }

    /// <summary>Asks the embedder for the vector of the title and body; a text the embedder refuses is logged and indexed without a vector.</summary>
    private async Task AddVectorAsync(SearchDocument document, string documentKey, CancellationToken stoppingToken)
    {
        string? text = SearchDocumentMapper.TextToEmbed(document);
        if (text == null)
        {
            return;
        }
        (float[]? vector, string? refusal) = await _embedderClient.EmbedAsync(text, stoppingToken);
        if (refusal != null)
        {
            _logger.LogWarning("{DocumentKey} is indexed without a vector: {Reason}", documentKey, refusal);
        }
        document.TextVector = vector;
    }

    /// <summary>Writes the document (true); a document Elasticsearch refuses is logged and skipped (false).</summary>
    private async Task<bool> WriteAsync(SearchDocument document, string documentKey, CancellationToken stoppingToken)
    {
        string? refusal = await _elasticsearchRepository.WriteAsync(documentKey, document, stoppingToken);
        if (refusal != null)
        {
            _logger.LogError("Elasticsearch refused {DocumentKey}: {Reason}", documentKey, refusal);
            _pipelineLog.Failed($"Elasticsearch refused {documentKey}: {refusal}");
            return false;
        }
        return true;
    }
}
