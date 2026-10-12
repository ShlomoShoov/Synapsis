using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Synapsis.KafkaRelay;

/// <summary>
/// Monitors the MongoDB "records" collection for newly inserted documents using Change Streams.
/// </summary>
public sealed class MongoDocumentChangeMonitor : IDocumentChangeMonitor
{
    private readonly MongoContext _context;
    private readonly ILogger<MongoDocumentChangeMonitor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDocumentChangeMonitor"/> class.
    /// </summary>
    /// <param name="context">The MongoDB data access context.</param>
    /// <param name="logger">The logger instance.</param>
    public MongoDocumentChangeMonitor(MongoContext context, ILogger<MongoDocumentChangeMonitor> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WatchInsertsAsync(
        BsonDocument? resumeToken,
        Func<string, BsonDocument, CancellationToken, Task> onInsert,
        CancellationToken cancellationToken)
    {
        IMongoCollection<StoredRecord> collection = _context.Records;

        // Filter exclusively for insert operations
        PipelineDefinition<ChangeStreamDocument<StoredRecord>, ChangeStreamDocument<StoredRecord>> pipeline =
            new EmptyPipelineDefinition<ChangeStreamDocument<StoredRecord>>()
                .Match(change => change.OperationType == ChangeStreamOperationType.Insert);

        ChangeStreamOptions options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.Default
        };

        if (resumeToken != null)
        {
            _logger.LogInformation("Resuming Change Stream from stored resume token.");
            options.ResumeAfter = resumeToken;
        }
        else
        {
            _logger.LogInformation("No resume token found. Starting Change Stream at current cluster time (ignoring historical documents).");
        }

        using IChangeStreamCursor<ChangeStreamDocument<StoredRecord>> cursor = await collection.WatchAsync(pipeline, options, cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (ChangeStreamDocument<StoredRecord> change in cursor.Current)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract document ID from DocumentKey._id
                string documentId = change.DocumentKey["_id"].AsString;
                BsonDocument token = change.ResumeToken;

                _logger.LogDebug("Detected insert for document ID {DocumentId}", documentId);

                // Await callback processing before advancing to the next event
                await onInsert(documentId, token, cancellationToken);
            }
        }
    }
}
