namespace Synapsis.Sil.Vocabulary.Terms;

/// <summary>
/// A measure: its unit, whether it may go below zero, and its allowed range.
/// Example (FDIC): equity, in usd_k, signed (a bank's equity can be negative).
/// </summary>
public sealed class MeasureDefinition
{
    /// <summary>The measure's name in lower case, for example "deaths_best".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The one unit every value is in, for example "count", "usd_k" or "percent".</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>True when values may go below zero; counts never are. False when the package does not say.</summary>
    public bool Signed { get; init; }

    /// <summary>The smallest value allowed.</summary>
    public double? Min { get; init; }

    /// <summary>The largest value allowed.</summary>
    public double? Max { get; init; }
}
