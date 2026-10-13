using Microsoft.AspNetCore.Mvc;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Requests;
using Synapsis.MockApi.Services;

namespace Synapsis.MockApi.Controllers;

/// <summary>
/// The Analyze routes, as the real API has them: GET /analyze/map and GET /analyze/graph. ASP.NET reads and checks
/// the parameters (a wrong one answers 400 before the method runs); the method passes them to AnalyzeService.
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
    public ActionResult<MapAnswer> Map([FromQuery] MapRequest request)
    {
        return Ok(_analyzeService.Map(request));
    }

    /// <summary>GET /analyze/graph: 404 when no record has the id at as_of; 400 for a group the record does not have.</summary>
    [HttpGet("graph")]
    public ActionResult<GraphAnswer> Graph([FromQuery] GraphRequest request)
    {
        GraphAnswer? answer = _analyzeService.Graph(request);
        if (answer == null)
        {
            return Problem(statusCode: 404, detail: "No record " + request.Id + " is known at " + (request.AsOf?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") ?? "now") + ".");
        }
        if (request.Group != null && answer.Connections.Count == 0)
        {
            ModelState.AddModelError("group", "group '" + request.Group + "' is not one of this record's connection groups.");
            return ValidationProblem();
        }
        return Ok(answer);
    }
}
