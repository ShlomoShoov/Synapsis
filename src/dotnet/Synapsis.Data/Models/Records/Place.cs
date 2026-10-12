using System.Collections.Generic;

namespace Synapsis.Data.Models.Records;

/// <summary>Where something happened: one or more of a point, an area, a logical position; or unplaced.</summary>
public sealed class Place
{
    /// <summary>A point on the map, [longitude, latitude].</summary>
    public List<double> Point { get; set; } = new List<double>();

    /// <summary>How far, in kilometres, the real place may be from the point.</summary>
    public double? UncertaintyKm { get; set; }

    /// <summary>A country and maybe a subdivision.</summary>
    public AdminArea? Admin { get; set; }

    /// <summary>A position that is not on a map, such as vendor then product.</summary>
    public LogicalPlace? Logical { get; set; }

    /// <summary>True when the source gives no place at all.</summary>
    public bool? Unplaced { get; set; }
}
