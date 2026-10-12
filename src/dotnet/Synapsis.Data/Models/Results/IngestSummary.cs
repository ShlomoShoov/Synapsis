using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Results;

/// <summary>
/// What one batch brought: how many records were received, stored, unchanged, duplicated and refused, and why.
/// The same class has two jobs: the answer to POST /ingest (the whole batch, Source empty), and one document per
/// source in the Elasticsearch index "synapsis-ingest-log", which the UI reads. The JSON names below are the field
/// names of both. Always: received = new + changed + unchanged + duplicates + rejected.
/// </summary>
public sealed class IngestSummary
{
    /// <summary>When the API received the batch, in UTC.</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>The batch id the producer gave.</summary>
    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Who sent the batch, such as "feeder:kev".</summary>
    [JsonPropertyName("producer")]
    public string Producer { get; set; } = string.Empty;

    /// <summary>The source these counts are about (log documents only); empty in the answer, which covers every source.</summary>
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    /// <summary>How many records arrived.</summary>
    [JsonPropertyName("received")]
    public int Received { get; set; }

    /// <summary>Stored for the first time (state 1).</summary>
    [JsonPropertyName("new")]
    public int New { get; set; }

    /// <summary>Stored as the next state, because their content changed.</summary>
    [JsonPropertyName("changed")]
    public int Changed { get; set; }

    /// <summary>Already stored with the same content: nothing written.</summary>
    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }

    /// <summary>Exact copies of an earlier record in the same batch: dropped.</summary>
    [JsonPropertyName("duplicates")]
    public int Duplicates { get; set; }

    /// <summary>Refused, each with a code and a reason.</summary>
    [JsonPropertyName("rejected")]
    public int Rejected { get; set; }

    /// <summary>How long the API took for the batch, in milliseconds.</summary>
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    /// <summary>How many records each code refused.</summary>
    [JsonPropertyName("rejection_counts")]
    public List<CodeCount> RejectionCounts { get; set; } = new List<CodeCount>();

    /// <summary>The refused records (all of them in the answer; the first 50 in a log document).</summary>
    [JsonPropertyName("rejections")]
    public List<RejectedRecord> Rejections { get; set; } = new List<RejectedRecord>();
}
