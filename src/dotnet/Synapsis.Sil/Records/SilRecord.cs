using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

public enum RecordKind
{
    Entity,
    Event,
    Relation,
    Conclusion,
}

/// <summary>
/// The fields every SIL 1.2 record carries. Typed records are for reads and for the
/// engine's findings; validation reads raw JSON so unknown fields are refused, not dropped.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EntityRecord), "entity")]
[JsonDerivedType(typeof(EventRecord), "event")]
[JsonDerivedType(typeof(RelationRecord), "relation")]
[JsonDerivedType(typeof(ConclusionRecord), "conclusion")]
public abstract record SilRecord
{
    /// <summary>The record kind; written as the "kind" discriminator.</summary>
    [JsonIgnore]
    public RecordKind Kind => this switch
    {
        EntityRecord => RecordKind.Entity,
        EventRecord => RecordKind.Event,
        RelationRecord => RecordKind.Relation,
        ConclusionRecord => RecordKind.Conclusion,
        _ => throw new InvalidOperationException($"unknown record type {GetType().Name}"),
    };

    /// <summary><c>&lt;kind&gt;:&lt;namespace&gt;:&lt;key&gt;</c>, e.g. <c>evt:kev:CVE-2023-34362</c>.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; } 

    /// <summary>A code registered for this kind in a schema package.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>A registered source id, e.g. <c>src:cisa:kev</c>.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>0 to 100.</summary>
    [JsonPropertyName("reliability")]
    public required double Reliability { get; init; }

    /// <summary>A value from the core <c>reliability_basis</c> vocabulary, e.g. <c>A1</c>.</summary>
    [JsonPropertyName("reliability_basis")]
    public required string ReliabilityBasis { get; init; }

    [JsonPropertyName("known_at")]
    public required DateTimeOffset KnownAt { get; init; }

    /// <summary>A value from the core <c>known_at_basis</c> vocabulary, e.g. <c>published</c>.</summary>
    [JsonPropertyName("known_at_basis")]
    public required string KnownAtBasis { get; init; }

    [JsonPropertyName("text")]
    public TextBlock? Text { get; init; }

    /// <summary>Only the names the type declares in its package.</summary>
    [JsonPropertyName("attributes")]
    public JsonObject? Attributes { get; init; }

    [JsonPropertyName("evidence")]
    public Evidence? Evidence { get; init; }
}
