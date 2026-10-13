using System;
using System.Text.Json;

namespace Synapsis.Api.Models.Dto;

/// <summary>
/// One answer of GET /records/{id}: the record as one source stated it, in its latest state known at the date asked.
/// It is the stored record turned back into JSON (MongoDB keeps it as BSON, which the browser cannot read).
/// </summary>
public sealed class RecordAnswer
{
    /// <summary>The source that stated it.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The state number: 1 for the first copy, +1 each time the content changed.</summary>
    public int State { get; set; }

    /// <summary>When the source says this state was known, in UTC.</summary>
    public DateTime KnownAt { get; set; }

    /// <summary>When the API received it, in UTC.</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>The batch that brought it.</summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Who sent that batch.</summary>
    public string Producer { get; set; } = string.Empty;

    /// <summary>The record exactly as it arrived.</summary>
    public JsonElement Record { get; set; }
}
