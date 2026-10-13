using System.Collections.Generic;
using MongoDB.Bson;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Synapsis.KafkaRelay;

/// <summary>
/// Contract for monitoring MongoDB Change Stream events for newly inserted documents.
/// </summary>
public interface IDocumentChangeMonitor
{
    /// <summary>
    /// Watches the records collection for new document insertions and invokes the callback for each batch of events
    /// the server returns, in order.
    /// </summary>
    /// <param name="resumeToken">The MongoDB resume token to resume from, or null if starting fresh.</param>
    /// <param name="onInserts">Async callback invoked for each batch of inserts: each one's document ID and resume token.</param>
    /// <param name="cancellationToken">Cancellation token to signal shutdown.</param>
    Task WatchInsertsAsync(
        BsonDocument? resumeToken,
        Func<IReadOnlyList<(string DocumentId, BsonDocument ResumeToken)>, CancellationToken, Task> onInserts,
        CancellationToken cancellationToken);
}
