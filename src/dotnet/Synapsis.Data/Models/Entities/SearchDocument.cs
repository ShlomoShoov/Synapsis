using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Entities;

/// <summary>
/// One document of the Elasticsearch index of records ("synapsis-records"): one state of one entity, event or
/// conclusion, flattened into the fields the searches filter, sort and count by. Its id in Elasticsearch is the
/// MongoDB document key, "{id}|{source}|{state}". A value the record does not give stays null; a list it does not
/// give stays empty. The index mapping (Synapsis.Setup, CreateRecordsIndexStep) has exactly these JSON names.
/// </summary>
public sealed class SearchDocument
{
    // Identity: copied from the MongoDB envelope.

    /// <summary>The record's id, such as "evt:kev:CVE-2023-4966".</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The record without its state, "{id}|{source}": the latest state per record is found by this key.</summary>
    [JsonPropertyName("record_key")]
    public string RecordKey { get; set; } = string.Empty;

    /// <summary>The state number (1, 2, 3...).</summary>
    [JsonPropertyName("state")]
    public int State { get; set; }

    /// <summary>entity, event or conclusion (relations are not indexed).</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The record's type, such as "GEO.EARTHQUAKE".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>The source that stated it, such as "src:usgs:comcat".</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>When the source says this was known, in UTC: every search filters known_at at or before as_of.</summary>
    [JsonPropertyName("known_at")]
    public DateTime KnownAt { get; set; }

    // Text: what keyword and semantic search read.

    /// <summary>An entity's name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The title of an event or conclusion.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>The body of an event or conclusion.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>The meaning of the title and body as numbers (from the embedder); null when there is no text or no known word.</summary>
    [JsonPropertyName("text_vector")]
    public float[]? TextVector { get; set; }

    // Time: when an event happened.

    /// <summary>When it started, in UTC.</summary>
    [JsonPropertyName("time_start")]
    public DateTime? TimeStart { get; set; }

    /// <summary>How precise the start is: second, minute, hour, day, week, month, quarter or year.</summary>
    [JsonPropertyName("time_precision")]
    public string? TimePrecision { get; set; }

    /// <summary>The Monday of the week it started in, only when the precision is a week or finer.</summary>
    [JsonPropertyName("week")]
    public DateTime? Week { get; set; }

    // Place: where an event happened.

    /// <summary>The point, [longitude, latitude], exactly as in the record (Elasticsearch reads a geo_point in this order).</summary>
    [JsonPropertyName("location")]
    public List<double>? Location { get; set; }

    /// <summary>The H3 cell of the point at resolution 2 (edge about 158 km), only when the point is precise enough.</summary>
    [JsonPropertyName("h3_r2")]
    public string? H3R2 { get; set; }

    /// <summary>The H3 cell at resolution 3 (edge about 60 km), only when the point is precise enough.</summary>
    [JsonPropertyName("h3_r3")]
    public string? H3R3 { get; set; }

    /// <summary>The H3 cell at resolution 4 (edge about 23 km), only when the point is precise enough.</summary>
    [JsonPropertyName("h3_r4")]
    public string? H3R4 { get; set; }

    /// <summary>The H3 cell at resolution 5 (edge about 8.5 km), only when the point is precise enough.</summary>
    [JsonPropertyName("h3_r5")]
    public string? H3R5 { get; set; }

    /// <summary>The country code (ISO 3166-1).</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>The subdivision code (ISO 3166-2), such as "US-CA".</summary>
    [JsonPropertyName("subdivision")]
    public string? Subdivision { get; set; }

    /// <summary>The logical space, then the space with every step of the path, joined by "/".</summary>
    [JsonPropertyName("logical_prefixes")]
    public List<string> LogicalPrefixes { get; set; } = new List<string>();

    // Links: the numbers and the entities of an event.

    /// <summary>Each measure's values by its name (a name the event gives twice has two values).</summary>
    [JsonPropertyName("measures")]
    public Dictionary<string, List<double>> Measures { get; set; } = new Dictionary<string, List<double>>();

    /// <summary>The ids of the entities that took part, each once.</summary>
    [JsonPropertyName("entities")]
    public List<string> Entities { get; set; } = new List<string>();
}
