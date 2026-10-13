using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Services;

namespace Synapsis.Api.Controllers;

/// <summary>
/// The Analyze routes the UI reads: GET /analyze/map (events by place and time) and GET /analyze/point (a clicked
/// event or entity with its connections). ASP.NET reads and checks the parameters (a wrong one answers 400 before the
/// method runs); the method passes them to AnalyzeService and returns its answer. Until the queries are built, the
/// service says so and the route answers 501 Not Implemented.
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

    /// <summary>GET /analyze/point: a record in full, with its connections grouped by kind.</summary>
    [HttpGet("point")]
    public async Task<ActionResult<PointAnswer>> Point([FromQuery] PointRequest request)
    {
        try
        {
            PointAnswer answer = await _analyzeService.PointAsync(request);
            return Ok(answer);
        }
        catch (NotImplementedException notBuilt)
        {
            return Problem(statusCode: 501, detail: notBuilt.Message);
        }
    }
}
