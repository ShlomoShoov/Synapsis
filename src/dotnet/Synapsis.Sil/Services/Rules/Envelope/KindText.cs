using System.Text.Json;

namespace Synapsis.Sil.Services.Rules.Envelope;

/// <summary>Reads the "kind" field of a raw record, for the two envelope rules.</summary>
public static class KindText
{
    /// <summary>The record's "kind" text, or an empty text when it is missing or not text.</summary>
    public static string Of(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }
        if (!raw.TryGetProperty("kind", out JsonElement kind) || kind.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return kind.GetString() ?? string.Empty;
    }
}
