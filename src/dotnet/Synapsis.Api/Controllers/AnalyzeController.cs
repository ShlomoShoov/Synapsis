using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Services;

namespace Synapsis.Api.Controllers;

/// <summary>
/// The Analyze routes the UI reads: GET /analyze/map (events by place and time) and GET /analyze/graph (the
/// connections of a clicked event or entity; its full record is GET /records/{id}). ASP.NET reads and checks the
/// parameters (a wrong one answers 400 before the method runs); the method passes them to AnalyzeService and returns
/// its answer.
/// </summary>
[ApiController]
[Route("analyze")]
public sealed class AnalyzeController : ControllerBase
{
    private readonly AnalyzeService _analyzeService;

    /// <summary>Creates the controller over the service.</summary>
    public AnalyzeController(AnalyzeService analyzeService)
    {
        _analyzeService = analyzeService;
    }

    /// <summary>GET /analyze/map: the matching events in the map's box, as points or H3 cells.</summary>
    [HttpGet("map")]
    public async Task<ActionResult<MapAnswer>> Map([FromQuery] MapRequest request)
    {
        try
        {
            MapAnswer answer = await _analyzeService.MapAsync(request);
            return Ok(answer);
        }
        catch (NotImplementedException notBuilt)
        {
            return Problem(statusCode: 501, detail: notBuilt.Message);
        }
    }

    /// <summary>GET /analyze/graph: returns a record summary, or a built-in 404 or 400 problem answer.</summary>
    /// <remarks>
    /// as_of filters known_at on the current Neo4j graph; overwritten or stale links cannot reconstruct a historical
    /// topology. Graph event filtering uses the fields supported by GraphFilters, not every accepted shared filter.
    /// </remarks>
    [HttpGet("graph")]
    public async Task<ActionResult<GraphAnswer>> Graph([FromQuery] GraphRequest request)
    {
        try
        {
            GraphAnswer answer = await _analyzeService.GraphAsync(request);
            return Ok(answer);
        }
        catch (KeyNotFoundException missing)
        {
            return Problem(statusCode: 404, detail: missing.Message);
        }
        catch (ArgumentException invalidGroup) when (invalidGroup.ParamName == "group")
        {
            ModelState.AddModelError("group", invalidGroup.Message);
            return ValidationProblem();
        }
    }
}
