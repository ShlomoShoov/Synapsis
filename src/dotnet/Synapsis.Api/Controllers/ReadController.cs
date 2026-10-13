using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Repositories;

namespace Synapsis.Api.Controllers;

/// <summary>
/// The read API: five routes, each answering one thing, passing the stores' own answers through (Elasticsearch
/// documents and buckets, Neo4j nodes and links). Each action reads its parameters (400 when one is wrong) and calls
/// one repository method, where the query is written. Shared optional filters on the Elasticsearch routes: from, to
/// (dates, on time_start, to excluded), source and type (repeatable).
/// </summary>
[ApiController]
public sealed class ReadController : ControllerBase
{
    private readonly SearchRepository _searchRepository;
    private readonly GraphRepository _graphRepository;

    /// <summary>Creates the controller over Elasticsearch and Neo4j.</summary>
    public ReadController(SearchRepository searchRepository, GraphRepository graphRepository)
    {
        _searchRepository = searchRepository;
        _graphRepository = graphRepository;
    }

    /// <summary>GET /map/cells: records per H3 cell at a resolution (2 to 5), in the bbox when given.</summary>
    [HttpGet("map/cells")]
    public async Task<IActionResult> MapCells(int? resolution, string? bbox, DateTime? from, DateTime? to,
        [FromQuery] string[]? source, [FromQuery] string[]? type, string kind = "event")
    {
        if (resolution == null || resolution < 2 || resolution > 5)
        {
            return Problem(statusCode: 400, detail: "resolution is required, 2 to 5.");
        }
        double[]? box = bbox == null ? null : ParseBox(bbox);
        if (bbox != null && box == null)
        {
            return Problem(statusCode: 400, detail: "bbox must be minLon,minLat,maxLon,maxLat.");
        }
        return Ok(await _searchRepository.MapCellsAsync(resolution.Value, box, kind, from, to, source, type));
    }

    /// <summary>GET /map/points: the matching documents with a point inside the bbox (required), one per record, at most limit (5,000).</summary>
    [HttpGet("map/points")]
    public async Task<IActionResult> MapPoints(string? bbox, DateTime? from, DateTime? to,
        [FromQuery] string[]? source, [FromQuery] string[]? type, string kind = "event", int limit = 5000)
    {
        double[]? box = bbox == null ? null : ParseBox(bbox);
        if (box == null)
        {
            return Problem(statusCode: 400, detail: "bbox is required: minLon,minLat,maxLon,maxLat.");
        }
        if (limit < 1 || limit > 5000)
        {
            return Problem(statusCode: 400, detail: "limit must be 1 to 5000.");
        }
        return Ok(await _searchRepository.MapPointsAsync(box, kind, limit, from, to, source, type));
    }

    /// <summary>GET /graph: the node with this id and at most limit (200, max 1,000) of its direct links; 404 when no node has the id.</summary>
    [HttpGet("graph")]
    public async Task<IActionResult> Graph(string? id, int limit = 200)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Problem(statusCode: 400, detail: "id is required.");
        }
        if (limit < 1 || limit > 1000)
        {
            return Problem(statusCode: 400, detail: "limit must be 1 to 1000.");
        }
        JsonObject? graph = await _graphRepository.NeighboursAsync(id, limit);
        if (graph == null)
        {
            return Problem(statusCode: 404, detail: "No node has the id " + id + ".");
        }
        return Ok(graph);
    }

    /// <summary>GET /logical: one level of the logical tree (the children of node, or the top level), with documents and records per key.</summary>
    [HttpGet("logical")]
    public async Task<IActionResult> Logical(string? node, DateTime? from, DateTime? to, [FromQuery] string[]? source, [FromQuery] string[]? type)
    {
        return Ok(await _searchRepository.LogicalAsync(node, from, to, source, type));
    }

    /// <summary>
    /// GET /search: with q, every word as a prefix in name, title, body or id (or, with semantic=true, the nearest by
    /// meaning); without q, the filtered list, newest time_start first. One item per record, at most limit (50, max 500).
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, DateTime? from, DateTime? to, [FromQuery] string[]? source, [FromQuery] string[]? type,
        string? kind, string? logical, string? entity, bool semantic = false, int limit = 50)
    {
        if (limit < 1 || limit > 500)
        {
            return Problem(statusCode: 400, detail: "limit must be 1 to 500.");
        }
        if (semantic && string.IsNullOrWhiteSpace(q))
        {
            return Problem(statusCode: 400, detail: "semantic=true needs q.");
        }
        JsonObject? answer = await _searchRepository.SearchAsync(q, semantic, kind, logical, entity, limit, from, to, source, type);
        if (answer == null)
        {
            return Problem(statusCode: 400, detail: "The embedder refused the question.");
        }
        return Ok(answer);
    }

    /// <summary>"minLon,minLat,maxLon,maxLat" as four numbers in range; null when it is not.</summary>
    private static double[]? ParseBox(string text)
    {
        string[] parts = text.Split(',');
        if (parts.Length != 4)
        {
            return null;
        }
        double[] box = new double[4];
        for (int index = 0; index < 4; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out box[index]))
            {
                return null;
            }
        }
        bool longitudesOk = box[0] >= -180 && box[0] <= 180 && box[2] >= -180 && box[2] <= 180;
        bool latitudesOk = box[1] >= -90 && box[3] <= 90 && box[1] <= box[3];
        return longitudesOk && latitudesOk ? box : null;
    }
}
