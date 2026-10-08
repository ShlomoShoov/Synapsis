using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>
/// A claim derived from other records by a method: an engine finding, a classification
/// by a model, an analyst's assessment. Never a source's own fact.
/// </summary>
public sealed record ConclusionRecord : SilRecord
{
    /// <summary>The record ids the conclusion is about.</summary>
    [JsonPropertyName("about")]
    public required IReadOnlyList<string> About { get; init; }

    [JsonPropertyName("statement")]
    public required Statement Statement { get; init; }

    /// <summary>The record ids it was derived from.</summary>
    [JsonPropertyName("premises")]
    public required IReadOnlyList<string> Premises { get; init; }

    [JsonPropertyName("method")]
    public required Method Method { get; init; }

    /// <summary>0 to 100.</summary>
    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }

    [JsonPropertyName("time")]
    public TimeSpec? Time { get; init; }
}
