using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Requests;
using Synapsis.MockApi.Models.Results;

namespace Synapsis.MockApi.Services;

/// <summary>
/// The Analyze filters applied in memory to the sample, with the meaning the real API gives them in Elasticsearch
/// (Synapsis.Api SearchFilters): any value of a list filter matches, all filters must match, and a filter on a field
/// keeps only records that have that field.
/// </summary>
public static class SampleFilters
{
    /// <summary>Every filter except the map's place filters (bbox, cell), which InsidePlace applies.</summary>
    public static bool Matches(AnalyzeFilters filters, SampleItem item)
    {
        RecordSummary summary = item.Summary;

        // 1. The words, and what the record is.
        if (!HasWords(filters.Keywords, item) || !IsAnyOf(filters.Kinds, summary.Kind) || !IsAnyOf(filters.Types, summary.Type))
        {
            return false;
        }
        if (filters.Sources.Count > 0 && !filters.Sources.Exists(summary.Sources.Contains))
        {
            return false;
        }

        // 2. When it happened.
        if (filters.From != null && (summary.TimeStart == null || summary.TimeStart < FilterParser.Utc(filters.From.Value)))
        {
            return false;
        }
        if (filters.To != null && (summary.TimeStart == null || summary.TimeStart >= FilterParser.Utc(filters.To.Value)))
        {
            return false;
        }

        // 3. Where, by area and by logical node.
        if (!IsAnyOf(filters.Countries, summary.Country) || !IsAnyOf(filters.Subdivisions, summary.Subdivision) || !IsUnderAnyNode(filters.LogicalNodes, summary.Logical))
        {
            return false;
        }

        // 4. Who took part, and the numbers.
        return TakesPart(filters.Entities, summary.Participants) && MeasuresHold(filters.Measures, summary.Measures);
    }

    /// <summary>The map's place filters: the point is inside the box and in one of the cells (each when given).</summary>
    public static bool InsidePlace(AnalyzeFilters filters, SampleItem item)
    {
        List<double>? point = item.Summary.Point;
        if (filters.BoundingBox != null)
        {
            BoundingBox? box = FilterParser.ParseBoundingBox(filters.BoundingBox);
            if (point == null || box == null || !IsInside(box, point))
            {
                return false;
            }
        }
        if (filters.Cells.Count == 0)
        {
            return true;
        }
        foreach (string cell in filters.Cells)
        {
            int? resolution = FilterParser.CellResolution(cell);
            if (point != null && resolution != null && LatticeKeys.Cell(point, item.Summary.UncertaintyKm, resolution.Value) == cell)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>No words asked, every word among the record's words, or the whole text equal to the id.</summary>
    private static bool HasWords(string? keywords, SampleItem item)
    {
        if (string.IsNullOrWhiteSpace(keywords) || keywords.Trim() == item.Summary.Id)
        {
            return true;
        }
        foreach (string word in keywords.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!item.Words.Contains(word))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>No values asked, or the record's value is one of them (a record without the value does not match).</summary>
    private static bool IsAnyOf(List<string> values, string? value)
    {
        return values.Count == 0 || (value != null && values.Contains(value));
    }

    /// <summary>No nodes asked, or one of the record's nodes is a node asked or below it.</summary>
    private static bool IsUnderAnyNode(List<string> nodes, List<string> recordNodes)
    {
        if (nodes.Count == 0)
        {
            return true;
        }
        foreach (string recordNode in recordNodes)
        {
            foreach (string node in nodes)
            {
                if (recordNode == node || recordNode.StartsWith(node + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>No entities asked, or one of them takes part.</summary>
    private static bool TakesPart(List<string> entities, List<EntityLabel> participants)
    {
        return entities.Count == 0 || participants.Exists(participant => entities.Contains(participant.Id));
    }

    /// <summary>Every measure condition holds for some measure of that name.</summary>
    private static bool MeasuresHold(List<string> conditions, List<Measure> measures)
    {
        foreach (string text in conditions)
        {
            MeasureCondition? condition = FilterParser.ParseMeasure(text);
            if (condition == null || !measures.Exists(measure => measure.Name == condition.Name && Holds(condition, measure.Value)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The value compared with the condition's number by its operator.</summary>
    private static bool Holds(MeasureCondition condition, double? value)
    {
        if (value == null)
        {
            return false;
        }
        switch (condition.Operator)
        {
            case "gt": return value > condition.Value;
            case "gte": return value >= condition.Value;
            case "lt": return value < condition.Value;
            case "lte": return value <= condition.Value;
            default: return value == condition.Value;
        }
    }

    /// <summary>The point [longitude, latitude] is inside the box (a box whose west edge is east of its east edge crosses the 180th meridian).</summary>
    private static bool IsInside(BoundingBox box, List<double> point)
    {
        bool latitudeInside = point[1] >= box.MinLatitude && point[1] <= box.MaxLatitude;
        if (box.MinLongitude <= box.MaxLongitude)
        {
            return latitudeInside && point[0] >= box.MinLongitude && point[0] <= box.MaxLongitude;
        }
        return latitudeInside && (point[0] >= box.MinLongitude || point[0] <= box.MaxLongitude);
    }
}
