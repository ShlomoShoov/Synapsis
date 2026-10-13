using System.Collections.Generic;

namespace Synapsis.MockApi.Models.Dto;

/// <summary>One H3 hexagon of the map with how many matching events are in it; the corners are given so the UI needs no H3 library.</summary>
public sealed class MapCell
{
    /// <summary>The H3 cell id, such as "832a10fffffffff" (send it as the cell filter to list its events).</summary>
    public required string Cell { get; init; }

    /// <summary>The H3 resolution (2 to 5).</summary>
    public int Resolution { get; init; }

    /// <summary>Matching events in the cell.</summary>
    public int Count { get; init; }

    /// <summary>The cell's centre, [longitude, latitude].</summary>
    public List<double> Center { get; init; } = new List<double>();

    /// <summary>The cell's corners, each [longitude, latitude].</summary>
    public List<List<double>> Boundary { get; init; } = new List<List<double>>();
}
