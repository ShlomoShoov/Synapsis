using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>A typed edge between two entities, e.g. <c>PRODUCT_OF</c> or <c>ACQUIRED</c>.</summary>
public sealed record RelationRecord : SilRecord
{
    /// <summary>An entity id.</summary>
    [JsonPropertyName("from")]
    public required string From { get; init; }

    /// <summary>An entity id.</summary>
    [JsonPropertyName("to")]
    public required string To { get; init; }

    /// <summary>When the relation held, if the source says.</summary>
    [JsonPropertyName("time")]
    public TimeSpec? Time { get; init; }
}
