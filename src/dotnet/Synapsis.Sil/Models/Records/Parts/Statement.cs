using System.Text.Json;

namespace Synapsis.Sil.Models.Records.Parts;

/// <summary>
/// What a conclusion claims: a predicate, with either a target (Object) or a value.
/// Example: { "predicate": "in_logical_space", "object": "cyber.product_class/MFT" }.
/// </summary>
public sealed class Statement
{
    /// <summary>What kind of claim it is, in lower case, for example "in_logical_space" or "surprise".</summary>
    public required string Predicate { get; init; }

    /// <summary>The claim's target as text, for example a class in a closed logical space.</summary>
    public string? Object { get; init; }

    /// <summary>
    /// The claim's value as any JSON value, for example the observed and expected counts of an engine finding.
    /// </summary>
    public JsonElement? Value { get; init; }
}
