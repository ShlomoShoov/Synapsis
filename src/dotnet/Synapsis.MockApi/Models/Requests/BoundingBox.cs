namespace Synapsis.MockApi.Models.Requests;

/// <summary>The map's box read from "min_lon,min_lat,max_lon,max_lat", in degrees. A west edge east of the east edge crosses the 180th meridian.</summary>
public sealed class BoundingBox
{
    /// <summary>The west edge (longitude).</summary>
    public required double MinLongitude { get; init; }

    /// <summary>The south edge (latitude).</summary>
    public required double MinLatitude { get; init; }

    /// <summary>The east edge (longitude).</summary>
    public required double MaxLongitude { get; init; }

    /// <summary>The north edge (latitude).</summary>
    public required double MaxLatitude { get; init; }
}
