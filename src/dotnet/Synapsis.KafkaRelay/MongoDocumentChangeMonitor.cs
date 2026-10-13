using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;
using System;
using System.Collections.Generic;
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
        Func<IReadOnlyList<(string DocumentId, BsonDocument ResumeToken)>, CancellationToken, Task> onInserts,
        CancellationToken cancellationToken)
    {
        IMongoCollection<StoredRecord> collection = _context.Records;

        // Filter exclusively for insert operations; the relay needs only the document key, so the full document is left out
        PipelineDefinition<ChangeStreamDocument<StoredRecord>, ChangeStreamDocument<StoredRecord>> pipeline =
            new EmptyPipelineDefinition<ChangeStreamDocument<StoredRecord>>()
                .Match(change => change.OperationType == ChangeStreamOperationType.Insert)
                .AppendStage<ChangeStreamDocument<StoredRecord>, ChangeStreamDocument<StoredRecord>, ChangeStreamDocument<StoredRecord>>(
                    new BsonDocument("$project", new BsonDocument("fullDocument", 0)),
                    new ChangeStreamDocumentSerializer<StoredRecord>(MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<StoredRecord>()));

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
            List<(string DocumentId, BsonDocument ResumeToken)> inserts = new List<(string DocumentId, BsonDocument ResumeToken)>();
            foreach (ChangeStreamDocument<StoredRecord> change in cursor.Current)
            {
                // Extract document ID from DocumentKey._id
                inserts.Add((change.DocumentKey["_id"].AsString, change.ResumeToken));
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Await the batch's processing before advancing to the next batch
            if (inserts.Count > 0)
            {
                await onInserts(inserts, cancellationToken);
            }
        }
    }
}
