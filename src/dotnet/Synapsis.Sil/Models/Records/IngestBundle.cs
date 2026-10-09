using System.Collections.Generic;
using System.Text.Json;

namespace Synapsis.Sil.Models.Records;

/// <summary>
/// One ingest request: a batch of up to 5,000 records from one producer.
/// Example: { "sil": "1.2", "batch_id": "kev-d63270cc3cb80eb521183213", "producer": "feeder:kev", "records": [...] }
/// </summary>
public sealed class IngestBundle
{
    /// <summary>The version of the record language the producer speaks; always "1.2".</summary>
    public required string Sil { get; init; }

    /// <summary>The producer's name for this batch; it names the batch in the receipt and in the producer's logs.</summary>
    public required string BatchId { get; init; }

    /// <summary>Who sent the batch, for example "feeder:kev"; stored with every record it brings.</summary>
    public required string Producer { get; init; }

    /// <summary>
    /// The records, still raw JSON. Each one is checked on its own before it becomes a typed record, so that one bad
    /// record is refused with its own code while the others go on.
    /// </summary>
    public required List<JsonElement> Records { get; init; }
}
