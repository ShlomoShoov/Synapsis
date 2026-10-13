using System;
using System.Collections.Generic;
using H3;
using H3.Extensions;
using H3.Model;
using Synapsis.Api.Models.Dto;

namespace Synapsis.Api.Services;

/// <summary>The map's layers drawn from Elasticsearch's counts: the H3 cell size of a zoom, and each cell with its centre and corners.</summary>
public static class MapLayers
{
    /// <summary>The H3 resolution for a zoom: 0-3 resolution 2, 4-5 resolution 3, 6-7 resolution 4, 8 and more resolution 5.</summary>
    public static int ResolutionFor(double zoom)
    {
        if (zoom < 4)
        {
            return 2;
        }
        if (zoom < 6)
        {
            return 3;
        }
        if (zoom < 8)
        {
            return 4;
        }
        return 5;
    }

    /// <summary>The counted cells, each with its centre and corners, the largest count first (then by cell).</summary>
    public static List<MapCell> Cells(Dictionary<string, int> countByCell, int resolution)
    {
        List<MapCell> cells = new List<MapCell>();
        foreach (KeyValuePair<string, int> pair in countByCell)
        {
            cells.Add(CellOf(pair.Key, resolution, pair.Value));
        }
        cells.Sort(CompareByCount);
        return cells;
    }

    /// <summary>A cell with its centre and corners, [longitude, latitude], rounded to 4 decimals (about 10 m).</summary>
    private static MapCell CellOf(string cell, int resolution, int count)
    {
        H3Index index = new H3Index(cell);
        LatLng center = index.ToLatLng();
        List<List<double>> boundary = new List<List<double>>();
        foreach (LatLng corner in index.GetCellBoundaryVertices())
        {
            boundary.Add(new List<double> { Math.Round(corner.LongitudeDegrees, 4), Math.Round(corner.LatitudeDegrees, 4) });
        }
        List<double> centerPoint = new List<double> { Math.Round(center.LongitudeDegrees, 4), Math.Round(center.LatitudeDegrees, 4) };
        return new MapCell { Cell = cell, Resolution = resolution, Count = count, Center = centerPoint, Boundary = boundary };
    }

    /// <summary>Largest count first; then by cell id.</summary>
    private static int CompareByCount(MapCell first, MapCell second)
    {
        int byCount = second.Count.CompareTo(first.Count);
        return byCount != 0 ? byCount : string.CompareOrdinal(first.Cell, second.Cell);
    }
}
