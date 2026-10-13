using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Repositories;
using Synapsis.Data.Dal;

namespace Synapsis.Api.Controllers;

/// <summary>
/// The read API: five routes, each answering one thing, passing the stores' own answers through (Elasticsearch
/// documents and buckets, Neo4j nodes and links). The UI does all the shaping. Shared optional filters on the
/// Elasticsearch routes: from, to (dates, on time_start, to excluded), source and type (repeatable).
/// </summary>
[ApiController]
public sealed class ReadController : ControllerBase
{
    private readonly SearchRepository _searchRepository;
    private readonly GraphRepository _graphRepository;
    private readonly EmbedderClient _embedderClient;

    /// <summary>Creates the controller over Elasticsearch, Neo4j and the embedder.</summary>
    public ReadController(SearchRepository searchRepository, GraphRepository graphRepository, EmbedderClient embedderClient)
    {
        _searchRepository = searchRepository;
        _graphRepository = graphRepository;
        _embedderClient = embedderClient;
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
        List<JsonObject> filters = SearchRepository.Filters(from, to, source, type, kind);
        if (bbox != null)
        {
            double[]? box = ParseBox(bbox);
            if (box == null)
            {
                return Problem(statusCode: 400, detail: "bbox must be minLon,minLat,maxLon,maxLat.");
            }
            filters.Add(SearchRepository.InsideBox(box));
        }
        return Ok(await _searchRepository.MapCellsAsync(filters, resolution.Value));
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
