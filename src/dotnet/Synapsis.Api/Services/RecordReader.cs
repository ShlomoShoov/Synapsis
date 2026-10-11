using System.Text.Json;
using Synapsis.Api.Models.Records;

namespace Synapsis.Api.Services;

/// <summary>
/// Reads one record's JSON into the class of its kind (EntityRecord, EventRecord, RelationRecord, ConclusionRecord)
/// with System.Text.Json. Fields the class does not list are skipped here; they stay in the stored JSON.
/// </summary>
public static class RecordReader
{
    /// <summary>How record JSON is read: snake_case names (known_at is KnownAt).</summary>
    private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Reads the record as the class its "kind" names. Throws JsonException (the framework's own error) when it is not
    /// an object, the kind is unknown, or a field has the wrong type.
    /// </summary>
    public static SilRecord Read(JsonElement json)
    {
        string? kind = ReadText(json, "kind");
        switch (kind)
        {
            case "entity":
                return json.Deserialize<EntityRecord>(ReadOptions)!;
            case "event":
                return json.Deserialize<EventRecord>(ReadOptions)!;
            case "relation":
                return json.Deserialize<RelationRecord>(ReadOptions)!;
            case "conclusion":
                return json.Deserialize<ConclusionRecord>(ReadOptions)!;
            default:
                throw new JsonException("kind must be entity, event, relation or conclusion, not '" + kind + "'");
        }
    }

    /// <summary>A text field of the record, or null when the record is not an object or the field is not text.</summary>
    public static string? ReadText(JsonElement json, string field)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!json.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return value.GetString();
    }
}
