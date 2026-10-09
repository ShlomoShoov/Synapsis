using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Synapsis.Sil.Records;

namespace Synapsis.Sil.Reading;

/// <summary>
/// Turns SIL JSON into the typed record classes: a whole request into an <see cref="IngestBundle"/>, and one record
/// into the class of its kind. It reads; it does not judge. The record rules run before a record is read, so that
/// every problem gets the code of the rule that finds it.
/// </summary>
public static class SilReader
{
    /// <summary>
    /// The JSON settings, built once:
    /// C# property names map to snake_case JSON names (KnownAtBasis reads "known_at_basis");
    /// a JSON field the C# classes do not know is an error, never silently dropped;
    /// enum values are snake_case text ("entity", "event").
    /// </summary>
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Reads a request body into a bundle. Its records stay raw JSON, to be checked one by one.</summary>
    /// <exception cref="JsonException">The text is not a bundle (bad JSON, a missing or unknown field).</exception>
    public static IngestBundle ReadBundle(string json)
    {
        IngestBundle? bundle = JsonSerializer.Deserialize<IngestBundle>(json, Options);
        if (bundle == null)
        {
            throw new JsonException("The request body is JSON null, not a bundle.");
        }
        return bundle;
    }

    /// <summary>
    /// Reads one record into the class of its kind: <see cref="EntityRecord"/>, <see cref="EventRecord"/>,
    /// <see cref="RelationRecord"/> or <see cref="ConclusionRecord"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The record has no known "kind".</exception>
    /// <exception cref="JsonException">A field is missing, unknown or of the wrong type.</exception>
    public static SilRecord ReadRecord(JsonElement record)
    {
        string kind = KindOf(record);
        switch (kind)
        {
            case "entity":
                return ReadAs<EntityRecord>(record);
            case "event":
                return ReadAs<EventRecord>(record);
            case "relation":
                return ReadAs<RelationRecord>(record);
            case "conclusion":
                return ReadAs<ConclusionRecord>(record);
            default:
                throw new ArgumentException($"'{kind}' is not a SIL record kind.", nameof(record));
        }
    }

    /// <summary>Builds the settings described on <see cref="Options"/>.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    /// <summary>The value of the record's "kind" field, or an empty text when there is none.</summary>
    private static string KindOf(JsonElement record)
    {
        if (record.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }
        if (!record.TryGetProperty("kind", out JsonElement kind) || kind.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return kind.GetString() ?? string.Empty;
    }

    /// <summary>Reads the record into the given class.</summary>
    private static T ReadAs<T>(JsonElement record) where T : SilRecord
    {
        T? typed = record.Deserialize<T>(Options);
        if (typed == null)
        {
            throw new JsonException("The record is JSON null.");
        }
        return typed;
    }
}
