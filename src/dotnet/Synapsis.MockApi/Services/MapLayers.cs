using System;
using System.Collections.Generic;
using H3;
using H3.Extensions;
using H3.Model;
using Synapsis.Data.Services;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Results;

namespace Synapsis.MockApi.Services;

/// <summary>The two layers of the map: the located events as points (oldest first), or counted per H3 cell (largest first).</summary>
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

    /// <summary>The located events as points, oldest first (then by id).</summary>
    public static List<RecordSummary> Points(List<SampleItem> located)
    {
        List<RecordSummary> points = new List<RecordSummary>();
        foreach (SampleItem item in located)
        {
            points.Add(item.Summary);
        }
        points.Sort(CompareByTime);
        return points;
    }

    /// <summary>
    /// The located events counted per H3 cell of the resolution, largest first (then by cell). An event whose
    /// uncertainty is not smaller than the cell's edge has no cell: it is counted in tooImprecise.
    /// </summary>
    public static List<MapCell> Cells(List<SampleItem> located, int resolution, out int tooImprecise)
    {
        // 1. Count the events of each cell.
        Dictionary<string, int> countByCell = new Dictionary<string, int>();
        tooImprecise = 0;
        foreach (SampleItem item in located)
        {
            string? cell = LatticeKeys.Cell(item.Summary.Point!, item.Summary.UncertaintyKm, resolution);
            if (cell == null)
            {
                tooImprecise++;
                continue;
            }
            countByCell[cell] = countByCell.ContainsKey(cell) ? countByCell[cell] + 1 : 1;
        }

        // 2. Each cell with its centre and corners, the largest count first.
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

    /// <summary>Oldest start first; then by id.</summary>
    private static int CompareByTime(RecordSummary first, RecordSummary second)
    {
        int byTime = Nullable.Compare(first.TimeStart, second.TimeStart);
        return byTime != 0 ? byTime : string.CompareOrdinal(first.Id, second.Id);
    }

    /// <summary>Largest count first; then by cell id.</summary>
    private static int CompareByCount(MapCell first, MapCell second)
    {
        int byCount = second.Count.CompareTo(first.Count);
        return byCount != 0 ? byCount : string.CompareOrdinal(first.Cell, second.Cell);
    }
}
