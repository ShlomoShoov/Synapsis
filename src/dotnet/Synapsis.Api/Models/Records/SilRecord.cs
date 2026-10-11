using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Api.Models.Records;

/// <summary>
/// What every SIL record has, whatever its kind. Only the fields the API checks are here; the rest of the record
/// (place, measures, attributes, text...) is not read, and is stored exactly as it arrived (<see cref="Json"/>).
/// Every field may be missing in the JSON, so each is nullable; the validation step says which are required.
/// </summary>
public abstract class SilRecord
{
    /// <summary>entity, event, relation or conclusion.</summary>
    public string? Kind { get; set; }

    /// <summary>The record's id, such as "evt:kev:CVE-2021-44228".</summary>
    public string? Id { get; set; }

    /// <summary>The record's type, such as "VULN.EXPLOITED".</summary>
    public string? Type { get; set; }

    /// <summary>The source that states it, such as "src:cisa:kev".</summary>
    public string? Source { get; set; }

    /// <summary>When the source says this was known. Not a date-time: the record cannot be read.</summary>
    public DateTimeOffset? KnownAt { get; set; }

    /// <summary>The record's position in the batch, from 0 (set by the API, not read from JSON).</summary>
    [JsonIgnore]
    public int Index { get; set; }

    /// <summary>The record exactly as it arrived (set by the API, not read from JSON).</summary>
    [JsonIgnore]
    public JsonElement Json { get; set; }
}
