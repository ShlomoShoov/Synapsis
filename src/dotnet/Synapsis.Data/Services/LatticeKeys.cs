using System;
using System.Collections.Generic;
using H3;
using H3.Extensions;
using H3.Model;
using NetTopologySuite.Geometries;
using Synapsis.Data.Models.Records;

namespace Synapsis.Data.Services;

/// <summary>
/// The lattice keys of a record: the H3 map cells of its point, the week it started in, and the prefixes of its logical
/// path. Computed one way only, so the search, the map and later the engine name a cell and a week the same way. Pure
/// code; the indexer uses it now (it can move into the API's enrichment later).
/// </summary>
public static class LatticeKeys
{
    /// <summary>The cell edge in km at resolutions 2 to 5: a point is placed in a cell only when its uncertainty is smaller.</summary>
    private static readonly Dictionary<int, double> EdgeKm = new Dictionary<int, double> { [2] = 158.2, [3] = 59.8, [4] = 22.6, [5] = 8.5 };

    /// <summary>The precisions fine enough to name a week.</summary>
    private static readonly HashSet<string> WeekPrecisions = new HashSet<string> { "second", "minute", "hour", "day", "week" };

    /// <summary>
    /// The H3 cell of a point at a resolution (2 to 5), such as "833f44fffffffff"; null when there is no point, no
    /// uncertainty, or an uncertainty not smaller than the cell's edge (a 50 km guess must not look like an 8 km fact).
    /// </summary>
    public static string? Cell(List<double> point, double? uncertaintyKm, int resolution)
    {
        // 1. A cell needs a point and an uncertainty smaller than the cell's edge.
        if (point.Count < 2 || uncertaintyKm == null)
        {
            return null;
        }
        if (uncertaintyKm.Value >= EdgeKm[resolution])
        {
            return null;
        }

        // 2. The record's point is [longitude, latitude]; a Coordinate is (x: longitude, y: latitude), in degrees.
        LatLng latLng = LatLng.FromCoordinate(new Coordinate(point[0], point[1]));
        return H3Index.FromLatLng(latLng, resolution).ToString();
    }

    /// <summary>
    /// The centre of an H3 cell, [longitude, latitude] like a record's point, and the cell named at resolutions 2 to 5:
    /// itself at its own resolution, its parent at each coarser one, nothing finer. Null when the text is not an H3
    /// cell. For a conclusion about a map cell (the indexer places it there).
    /// </summary>
    public static (List<double> Centre, Dictionary<int, string> Cells)? CellCentreAndParents(string cell)
    {
        // 1. Only a valid cell: a text that is not hexadecimal reads as 0, which is not one.
        H3Index index = new H3Index(cell);
        if (!index.IsValidCell)
        {
            return null;
        }

        // 2. The centre; a Coordinate is (x: longitude, y: latitude), in degrees.
        Coordinate centre = index.ToLatLng().ToCoordinate();
        List<double> point = new List<double> { centre.X, centre.Y };

        // 3. The cell and its parents, at the resolutions the index keeps (2 to 5) that are not finer than the cell.
        Dictionary<int, string> cells = new Dictionary<int, string>();
        foreach (int resolution in EdgeKm.Keys)
        {
            if (resolution <= index.Resolution)
            {
                cells[resolution] = index.GetParentForResolution(resolution).ToString();
            }
        }
        return (point, cells);
    }

    /// <summary>
    /// The Monday (UTC, at midnight) of the week the start falls in; null without a start, or when the precision is
    /// coarser than a week (month, quarter, year), because such a start does not say which week.
    /// </summary>
    public static DateTime? Week(DateTime? start, string? precision)
    {
        if (start == null || precision == null || !WeekPrecisions.Contains(precision))
        {
            return null;
        }
        DateTime day = DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc);
        int daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-daysSinceMonday);
    }

    /// <summary>
    /// The space, then the space with every step of the path, joined by "/": ["cyber.product_class",
    /// "cyber.product_class/network_device"]. Empty without a logical place.
    /// </summary>
    public static List<string> LogicalPrefixes(LogicalPlace? logical)
    {
        List<string> prefixes = new List<string>();
        if (logical == null || string.IsNullOrEmpty(logical.Space))
        {
            return prefixes;
        }
        string prefix = logical.Space;
        prefixes.Add(prefix);
        foreach (string step in logical.Path)
        {
            prefix = prefix + "/" + step;
            prefixes.Add(prefix);
        }
        return prefixes;
    }
}
