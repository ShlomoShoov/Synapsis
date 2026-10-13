namespace Synapsis.Api.Models.Dto;

/// <summary>Matching events that cannot be drawn, how many, and why: nothing is dropped silently.</summary>
public sealed class OmittedCount
{
    /// <summary>no_point (placed by region or logically only, or unplaced) or too_imprecise (the uncertainty is larger than the cell).</summary>
    public required string Reason { get; init; }

    /// <summary>How many events.</summary>
    public int Count { get; init; }

    /// <summary>A sentence for the user.</summary>
    public required string Message { get; init; }
}
