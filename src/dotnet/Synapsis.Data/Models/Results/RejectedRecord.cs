using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Results;

/// <summary>A record the API refused: where it was in the batch, which record it was, and why.</summary>
public sealed class RejectedRecord
{
    /// <summary>Its position in the batch, from 0.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Its id, when it had a readable one.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Its source, or "unknown" when it had no readable one.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>A short code for the reason, such as "E-MISSING", so refusals can be counted.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>The reason in plain words, such as "known_at is missing".</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
