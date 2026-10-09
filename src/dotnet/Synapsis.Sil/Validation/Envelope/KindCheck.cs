using System.Text.Json;
using Synapsis.Sil.Records;

namespace Synapsis.Sil.Validation.Envelope;

/// <summary>
/// Rule 1: the record's "kind" must be entity, event, relation or conclusion. A source is declared in a schema
/// package, never sent as a record. Every later rule depends on the kind, so this runs first.
/// </summary>
public static class KindCheck
{
    /// <summary>Returns E-KIND when the kind is missing or unknown; otherwise null, and the kind.</summary>
    public static RecordError? Check(JsonElement record, out RecordKind kind)
    {
        kind = RecordKind.Entity;
        string text = KindText(record);
        switch (text)
        {
            case "entity":
                kind = RecordKind.Entity;
                return null;
            case "event":
                kind = RecordKind.Event;
                return null;
            case "relation":
                kind = RecordKind.Relation;
                return null;
            case "conclusion":
                kind = RecordKind.Conclusion;
                return null;
            default:
                return new RecordError(ErrorCodes.Kind, "kind",
                    "kind must be entity, event, relation or conclusion; sources are declared in schema packages");
        }
    }

    /// <summary>The record's "kind" text, or an empty text when there is none.</summary>
    private static string KindText(JsonElement record)
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
}
