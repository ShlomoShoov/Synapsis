using System;
using System.Threading.Tasks;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Repositories;

namespace Synapsis.Api.Services;

/// <summary>
/// The logic of the Analyze routes: which stores to ask, in what order, and how to shape the answer. Elasticsearch
/// (SearchRepository) answers the map and the counts, Neo4j (GraphRepository) the connections, MongoDB
/// (RecordRepository) the participants of an event with their roles. The queries are the next task: until then each method says it is not built.
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
    /// GET /analyze/map. The planned flow: 1. choose the layer (cells when zoomed out, below zoom 6, or when more
    /// than 2,000 located events match the filters; otherwise points); 2. ask Elasticsearch for the page of points or cell
    /// counts; 3. count what cannot be drawn (no point, too imprecise for the cell); 4. answer with the next cursor.
    /// </summary>
    public Task<MapAnswer> MapAsync(MapRequest request)
    {
        throw new NotImplementedException("GET /analyze/map is not built yet.");
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
}
