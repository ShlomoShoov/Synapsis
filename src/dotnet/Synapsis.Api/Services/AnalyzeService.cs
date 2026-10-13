using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Models.Results;
using Synapsis.Api.Repositories;

namespace Synapsis.Api.Services;

/// <summary>
/// The logic of the Analyze routes: which stores to ask, in what order, and how to shape the answer. Elasticsearch
/// (SearchRepository) answers the map and the counts, Neo4j (GraphRepository) the connections, MongoDB
/// (RecordRepository) the participants of an event with their roles. The map's cells layer is built; its points layer
/// and the graph say they are not built yet.
/// </summary>
public sealed class AnalyzeService
{
    private readonly SearchRepository _searchRepository;
    private readonly GraphRepository _graphRepository;
    private readonly RecordRepository _recordRepository;

    /// <summary>Creates the service over the three stores: Elasticsearch (map, search, counts), Neo4j (connections), MongoDB (an event's participants and roles).</summary>
    public AnalyzeService(SearchRepository searchRepository, GraphRepository graphRepository, RecordRepository recordRepository)
    {
        _searchRepository = searchRepository;
        _graphRepository = graphRepository;
        _recordRepository = recordRepository;
    }

    /// <summary>
    /// GET /analyze/map: the matching events as the layer asked for (ASP.NET has already answered 400 to a missing
    /// or unknown layer). The service builds no query: it asks SearchRepository and shapes the answer.
    /// </summary>
    public async Task<MapAnswer> MapAsync(MapRequest request)
    {
        if (request.Layer == "points")
        {
            throw new NotImplementedException("The points layer of GET /analyze/map is not built yet.");
        }
        return await CellsAsync(request);
    }

    /// <summary>
    /// GET /analyze/graph. The planned flow: 1. find the record known at as_of (none: 404); 2. find its connections
    /// (relations and shared events in Neo4j, the events it takes part in from Elasticsearch, the participants and
    /// their roles of an event from its record in MongoDB); 3. group them by kind; 4. none: say why.
    /// </summary>
    public Task<GraphAnswer> GraphAsync(GraphRequest request)
    {
        throw new NotImplementedException("GET /analyze/graph is not built yet.");
    }

    /// <summary>The cells layer: the H3 cells at the zoom's resolution with their event counts, the largest first.</summary>
    private async Task<MapAnswer> CellsAsync(MapRequest request)
    {
        // 1. The cell size from the zoom.
        int resolution = MapLayers.ResolutionFor(request.Zoom ?? 0);

        // 2. Elasticsearch counts the events per cell, and what cannot be drawn.
        CellCounts counts = await _searchRepository.CellCountsAsync(request, resolution);
        int withoutPoint = await _searchRepository.CountWithoutPointAsync(request);
        int tooImprecise = await _searchRepository.CountTooImpreciseAsync(request, resolution);

        // 3. Each cell with its centre and corners; one page of at most limit cells, no next page (no paging yet).
        List<MapCell> cells = MapLayers.Cells(counts.CountByCell, resolution);
        return new MapAnswer
        {
            AsOf = AsOfOf(request), Layer = "cells", Resolution = resolution, Events = counts.Events, Total = counts.TotalCells,
            Next = null, Omitted = Omitted(withoutPoint, tooImprecise), Cells = cells,
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

    /// <summary>The as_of of the request in UTC; now when none was given.</summary>
    private static DateTime AsOfOf(AnalyzeFilters filters)
    {
        return filters.AsOf != null ? FilterParser.Utc(filters.AsOf.Value) : DateTime.UtcNow;
    }
}
