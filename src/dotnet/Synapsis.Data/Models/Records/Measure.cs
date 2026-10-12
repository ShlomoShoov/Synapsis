namespace Synapsis.Data.Models.Records;

/// <summary>A number the source gives about an event, with its unit.</summary>
public sealed class Measure
{
    /// <summary>The measure's name, such as "magnitude".</summary>
    public string? Name { get; set; }

    /// <summary>The value.</summary>
    public double? Value { get; set; }

    /// <summary>The unit, such as "count" or "usd_k".</summary>
    public string? Unit { get; set; }
}
