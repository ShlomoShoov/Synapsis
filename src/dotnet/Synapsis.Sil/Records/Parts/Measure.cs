namespace Synapsis.Sil.Records.Parts;

/// <summary>
/// One measured value of an event. A value the source does not give is left out, never sent as zero.
/// Example: { "name": "magnitude", "value": 7.5, "unit": "magnitude" }.
/// </summary>
public sealed class Measure
{
    /// <summary>The measure's name, as registered in a schema package and allowed by the event's type.</summary>
    public required string Name { get; init; }

    /// <summary>The value, as the source published it.</summary>
    public required double Value { get; init; }

    /// <summary>
    /// The unit the producer believes it is sending; it must equal the registered unit, which catches mapping
    /// mistakes such as dollars sent where the source means thousands of dollars.
    /// </summary>
    public required string Unit { get; init; }
}
