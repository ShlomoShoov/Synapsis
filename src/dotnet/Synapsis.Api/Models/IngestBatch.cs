using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Synapsis.Api.Models;

/// <summary>
/// The body of POST /ingest: a batch of records from one producer. ASP.NET reads it from JSON (gzip allowed) and
/// answers 400 by itself when a field below is missing or there are too many records. The records stay as raw JSON
/// here: each one is read as its own kind later, so one bad record does not refuse the whole batch.
/// </summary>
public sealed class IngestBatch
{
    /// <summary>The producer's id for this batch.</summary>
    [Required]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Who sends the batch, such as "feeder:kev".</summary>
    [Required]
    public string Producer { get; set; } = string.Empty;

    /// <summary>The records, as raw JSON objects: at most 5,000.</summary>
    [Required]
    [MaxLength(5000)]
    public List<JsonElement> Records { get; set; } = new List<JsonElement>();
}
