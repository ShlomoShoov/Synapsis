using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Synapsis.Data.Models.Records;

namespace Synapsis.MockApi.Models.Entities;

/// <summary>
/// One state of one record from one source, as Sample/records.json holds it (the shape of a MongoDB "records"
/// document, exported by Sample/export-sample.sh). Read once when the mock starts.
/// </summary>
public sealed class SampleRecord
{
    /// <summary>The document key, "{id}|{source}|{state}".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The record's id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The source that stated it.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>entity, event, relation or conclusion.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The record's type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The state number: 1, then 2, 3... each time the source changed its content.</summary>
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

    /// <summary>The record read into its class (by RecordReader when the sample is loaded); not in the file.</summary>
    [JsonIgnore]
    public SilRecord? ReadRecord { get; set; }
}
