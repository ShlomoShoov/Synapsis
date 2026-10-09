namespace Synapsis.Sil.Packages;

/// <summary>
/// The four forms of place an event type can allow. Fixed by the package format, so it is a C# enum.
/// In YAML: point, admin, logical, unplaced.
/// </summary>
public enum PlaceForm
{
    /// <summary>A point on the map with its uncertainty.</summary>
    Point,

    /// <summary>A country and optionally a subdivision, by ISO code.</summary>
    Admin,

    /// <summary>A position in a logical space.</summary>
    Logical,

    /// <summary>Deliberately no place.</summary>
    Unplaced,
}
