namespace Synapsis.Sil.Records.Parts;

/// <summary>
/// Where an event happened. It holds one or more of four forms, and the event's type says which forms it allows:
/// a point on the map with its uncertainty, an administrative area, a position in a logical space, or "unplaced".
/// </summary>
public sealed class Place
{
    /// <summary>A point on the map as [longitude, latitude], in degrees.</summary>
    public double[]? Point { get; init; }

    /// <summary>
    /// How far, in kilometres, the true location may be from the point. It decides which map cells the event may
    /// claim, so a country centroid never pretends to be a city. Present exactly when Point is present.
    /// </summary>
    public double? UncertaintyKm { get; init; }

    /// <summary>A country, and optionally a subdivision such as a US state, by ISO code.</summary>
    public AdminArea? Admin { get; init; }

    /// <summary>A position that is not on a map, for example vendor then product, or a product class.</summary>
    public LogicalPlace? Logical { get; init; }

    /// <summary>True when the event deliberately has no place (a financial series); then no other form is allowed.</summary>
    public bool? Unplaced { get; init; }
}
