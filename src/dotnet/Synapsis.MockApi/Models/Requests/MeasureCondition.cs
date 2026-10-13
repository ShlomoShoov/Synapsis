namespace Synapsis.MockApi.Models.Requests;

/// <summary>One measure filter read from "name:op:value", such as magnitude:gte:5.</summary>
public sealed class MeasureCondition
{
    /// <summary>The measure's name, such as "magnitude".</summary>
    public required string Name { get; init; }

    /// <summary>gt, gte, lt, lte or eq.</summary>
    public required string Operator { get; init; }

    /// <summary>The number to compare with.</summary>
    public required double Value { get; init; }
}
