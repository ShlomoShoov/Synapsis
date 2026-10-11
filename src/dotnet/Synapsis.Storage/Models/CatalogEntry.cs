using System;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Synapsis.Storage.Models;

/// <summary>
/// One value that has entered the system, such as the source "src:cisa:kev" or the event type "VULN.EXPLOITED".
/// Stored in the MongoDB collection "catalog" and listed by GET /catalog, so the UI can show and filter by them.
/// </summary>
public sealed class CatalogEntry
{
    /// <summary>The document id, "{field}:{value}".</summary>
    [BsonId]
    [JsonIgnore]
    public string Key { get; set; } = string.Empty;

    /// <summary>What the value is: "source", "entity_type", "event_type", "relation_type" or "conclusion_type".</summary>
    [BsonElement("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>The value itself.</summary>
    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>How many record states were stored with this value.</summary>
    [BsonElement("count")]
    public long Count { get; set; }

    /// <summary>When a record with this value was last stored, in UTC.</summary>
    [BsonElement("last_seen")]
    public DateTime LastSeen { get; set; }
}
