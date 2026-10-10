using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Synapsis.Storage.Models;

/// <summary>
/// One line of the ingestion log: what one batch brought from one source. A batch with records from two sources gives
/// two entries. Stored as a document in the Elasticsearch index "synapsis-ingest-log" and
/// read by the UI directly; the JSON names below are the document's field names (contracts/synapsis-api.yaml).
/// </summary>
public sealed class IngestLogEntry
{
    /// <summary>When the API received the batch (UTC).</summary>
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    /// <summary>The batch id the producer gave.</summary>
    [JsonPropertyName("batch_id")]
    public required string BatchId { get; init; }

    /// <summary>Who sent the batch, such as "feeder:kev".</summary>
    [JsonPropertyName("producer")]
    public required string Producer { get; init; }

    /// <summary>The source the records name, such as "src:cisa:kev"; "unknown" when a refused record had no readable source.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>How many records of this source the batch held.</summary>
    [JsonPropertyName("received")]
    public int Received { get; set; }

    /// <summary>Records stored for the first time (version 1).</summary>
    [JsonPropertyName("new")]
    public int New { get; set; }

    /// <summary>Records stored as a new version of what was there.</summary>
    [JsonPropertyName("changed")]
    public int Changed { get; set; }

    /// <summary>Records identical to what was already stored: nothing written.</summary>
    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }

    /// <summary>Exact copies inside the same batch, dropped.</summary>
    [JsonPropertyName("duplicates")]
    public int Duplicates { get; set; }

    /// <summary>Records refused with an error code.</summary>
    [JsonPropertyName("rejected")]
    public int Rejected { get; set; }

    /// <summary>How many records each error code refused.</summary>
    [JsonPropertyName("rejection_counts")]
    public List<RejectionCount> RejectionCounts { get; init; } = new List<RejectionCount>();

    /// <summary>The first refused records in detail (at most 50 per entry, to keep the log small).</summary>
    [JsonPropertyName("rejections")]
    public List<LoggedRejection> Rejections { get; init; } = new List<LoggedRejection>();

    /// <summary>How long the API took to handle the whole batch, in milliseconds.</summary>
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}
