using System.Text.Json;
using MongoDB.Bson;
using Synapsis.Data.Models.Records;

namespace Synapsis.Data.Services;

/// <summary>
/// Converts one SIL record between JSON and its class. It handles only the record itself: in MongoDB that is the
/// "record" field of a StoredRecord; the envelope around it (key, state, hash, batch...) stays a StoredRecord.
/// </summary>
public static class RecordReader
{
    /// <summary>
    /// How record JSON is read: snake_case names (known_at is KnownAt); unknown fields are skipped; times are UTC
    /// (a time without an offset, such as "2024-01-01", is taken as UTC on every machine).
    /// </summary>
    private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new UtcTimeConverter() }
    };

    /// <summary>
    /// Reads the record as the class its "kind" names (EntityRecord, EventRecord, RelationRecord, ConclusionRecord).
    /// Answers the record, or null and the reason it cannot be read (not an object, an unknown kind, a field of the
    /// wrong type). Unknown fields are skipped.
    /// </summary>
    public static (SilRecord? Record, string? Error) Read(JsonElement json)
    {
        // 1. The kind picks the class.
        string? kind = null;
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("kind", out JsonElement kindValue) && kindValue.ValueKind == JsonValueKind.String)
        {
            kind = kindValue.GetString();
        }

        // 2. Convert into that class; the framework's message says which field could not be read.
        try
        {
            switch (kind)
            {
                case "entity":
                    return (json.Deserialize<EntityRecord>(ReadOptions), null);
                case "event":
                    return (json.Deserialize<EventRecord>(ReadOptions), null);
                case "relation":
                    return (json.Deserialize<RelationRecord>(ReadOptions), null);
                case "conclusion":
                    return (json.Deserialize<ConclusionRecord>(ReadOptions), null);
                default:
                    return (null, "kind must be entity, event, relation or conclusion, not '" + kind + "'");
            }
        }
        catch (JsonException problem)
        {
            return (null, problem.Message);
        }
    }

    /// <summary>
    /// A record stored in MongoDB (BSON) as plain JSON, the same as it arrived. MongoDB first turns the BSON into plain
    /// .NET values (dictionaries, lists, numbers, text), then System.Text.Json writes them: numbers come back as they
    /// were sent (137.27, not MongoDB's own 137.27000000000001 or NumberLong(...)).
    /// </summary>
    public static JsonElement ToJson(BsonDocument storedRecord)
    {
        object? plainValues = BsonTypeMapper.MapToDotNetValue(storedRecord);
        return JsonSerializer.SerializeToElement(plainValues);
    }
}
