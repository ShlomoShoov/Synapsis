using System;
using System.Collections.Generic;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Entities;
using Synapsis.MockApi.Models.Requests;
using Synapsis.MockApi.Models.Results;
using Synapsis.MockApi.Repositories;

namespace Synapsis.MockApi.Services;

/// <summary>
/// The logic of the Analyze routes on the sample, in memory: the same answers the real API will give from
/// Elasticsearch and Neo4j. Every request starts from what was known at its as_of.
/// </summary>
public sealed class AnalyzeService
{
    private readonly SampleRepository _sampleRepository;

    /// <summary>Creates the service over the sample.</summary>
    public AnalyzeService(SampleRepository sampleRepository)
    {
        _sampleRepository = sampleRepository;
    }

    /// <summary>GET /analyze/map: the matching events in the map's box, as points or H3 cells.</summary>
    public MapAnswer Map(MapRequest request)
    {
        // 1. What was known at as_of: one item per record.
        DateTime asOf = AsOfOf(request);
        Dictionary<string, SampleItem> items = SampleItems.Build(_sampleRepository.LatestAt(asOf));

        // 2. The matching events: with a point inside the map's place filters, or with no point at all.
        List<SampleItem> located = new List<SampleItem>();
        int withoutPoint = 0;
        foreach (SampleItem item in items.Values)
        {
            if (item.Summary.Kind != "event" || !SampleFilters.Matches(request, item))
            {
                continue;
            }
            if (item.Summary.Point == null)
            {
                withoutPoint++;
            }
            else if (SampleFilters.InsidePlace(request, item))
            {
                located.Add(item);
            }
        }

        // 3. One page of the layer asked for (points or cells), and what cannot be drawn.
        return request.Layer == "cells" ? CellsAnswer(request, asOf, located, withoutPoint) : PointsAnswer(request, asOf, located, withoutPoint);
    }

    /// <summary>GET /analyze/graph: the record as a summary and its connections; null when no record has the id at as_of.</summary>
    public GraphAnswer? Graph(GraphRequest request)
    {
        // 1. What was known at as_of, and the record asked for.
        DateTime asOf = AsOfOf(request);
        List<SampleRecord> latest = _sampleRepository.LatestAt(asOf);
        Dictionary<string, SampleItem> items = SampleItems.Build(latest);
        if (request.Id == null || !items.ContainsKey(request.Id))
        {
            return null;
        }
        SampleItem center = items[request.Id];

        // 2. Its connections, grouped by kind.
        List<ConnectionGroup> groups = ConnectionFinder.Find(center, items, latest, request);

        // 3. The first page of every group, or the asked page of the one group asked for.
        List<ConnectionGroup> pages = new List<ConnectionGroup>();
        foreach (ConnectionGroup group in groups)
        {
            if (request.Group != null && group.Key != request.Group)
            {
                continue;
            }
            List<ConnectedRecord> page = Paging.Page(group.Items, request.Group != null ? request.Cursor : null, request.Limit, out string? next);
            pages.Add(new ConnectionGroup { Key = group.Key, Kind = group.Kind, Type = group.Type, Label = group.Label, Count = group.Count, Next = next, Items = page });
        }

        // 4. No connections: say why.
        string? reason = pages.Count == 0 ? WhyNoConnections(center, request) : null;
        return new GraphAnswer { AsOf = asOf, Center = center.Summary, Connections = pages, NoConnectionsReason = reason };
    }

    /// <summary>The points layer: one page of the located events, oldest first.</summary>
    private static MapAnswer PointsAnswer(MapRequest request, DateTime asOf, List<SampleItem> located, int withoutPoint)
    {
        List<RecordSummary> points = MapLayers.Points(located);
        List<RecordSummary> page = Paging.Page(points, request.Cursor, request.Limit, out string? next);
        return new MapAnswer
        {
            AsOf = asOf, Layer = "points", Events = points.Count, Total = points.Count, Next = next,
            Omitted = Omitted(withoutPoint, 0), Points = page,
        };
    }

    /// <summary>The cells layer: one page of the H3 cells at the zoom's resolution, largest count first.</summary>
    private static MapAnswer CellsAnswer(MapRequest request, DateTime asOf, List<SampleItem> located, int withoutPoint)
    {
        int resolution = MapLayers.ResolutionFor(request.Zoom ?? 0);
        List<MapCell> cells = MapLayers.Cells(located, resolution, out int tooImprecise);
        List<MapCell> page = Paging.Page(cells, request.Cursor, request.Limit, out string? next);
        return new MapAnswer
        {
            AsOf = asOf, Layer = "cells", Resolution = resolution, Events = located.Count - tooImprecise, Total = cells.Count,
            Next = next, Omitted = Omitted(withoutPoint, tooImprecise), Cells = page,
        };
    }

    /// <summary>What matched but cannot be drawn, each count with its reason; only the counts above zero.</summary>
    private static List<OmittedCount> Omitted(int withoutPoint, int tooImprecise)
    {
        List<OmittedCount> omitted = new List<OmittedCount>();
        if (withoutPoint > 0)
        {
            omitted.Add(new OmittedCount { Reason = "no_point", Count = withoutPoint, Message = withoutPoint + " matching events have no point (placed by region or logically only, or unplaced)." });
        }
        if (tooImprecise > 0)
        {
            omitted.Add(new OmittedCount { Reason = "too_imprecise", Count = tooImprecise, Message = tooImprecise + " matching events are too imprecise for cells of this size." });
        }
        return omitted;
    }

    /// <summary>Why a record has no connections, in one sentence.</summary>
    private static string WhyNoConnections(SampleItem center, AnalyzeFilters filters)
    {
        if (center.Summary.Kind == "event")
        {
            return "No entity takes part in this event, so it has no connections.";
        }
        if (center.Summary.Kind == "conclusion")
        {
            return "The links of a conclusion (what it is about, its premises) are not shown yet.";
        }
        if (HasFilters(filters))
        {
            return "This entity has no relations, and none of its events pass the current filters.";
        }
        return "No relation and no event of this entity is known at this as_of.";
    }

    /// <summary>True when any filter other than as_of is set.</summary>
    private static bool HasFilters(AnalyzeFilters filters)
    {
        bool anyList = filters.Kinds.Count + filters.Types.Count + filters.Sources.Count + filters.Cells.Count + filters.Countries.Count
            + filters.Subdivisions.Count + filters.LogicalNodes.Count + filters.Entities.Count + filters.Measures.Count > 0;
        return anyList || filters.Keywords != null || filters.From != null || filters.To != null || filters.BoundingBox != null;
    }

    /// <summary>The as_of of the request in UTC; now when none was given.</summary>
    private static DateTime AsOfOf(AnalyzeFilters filters)
    {
        return filters.AsOf != null ? FilterParser.Utc(filters.AsOf.Value) : DateTime.UtcNow;
    }
}
