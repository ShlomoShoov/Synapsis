using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Services.Http;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Api.Orchestrators;
using Synapsis.Api.Models.Ingestion;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Controllers;

/// <summary>POST /ingest: one batch of records (JSON, gzip allowed). With ?dry_run=true nothing is stored.</summary>
[ApiController]
public sealed class IngestController : ControllerBase
{
    private readonly IIngestionOrchestrator _ingestion;

    /// <summary>Creates the controller over the ingestion process.</summary>
    public IngestController(IIngestionOrchestrator ingestion)
    {
        _ingestion = ingestion;
    }

    /// <summary>200 with the receipt; or the problem with the whole request.</summary>
    [HttpPost("ingest")]
    [ServiceFilter(typeof(ApiKeyFilter))]
    public async Task<IActionResult> Post([FromQuery(Name = "dry_run")] bool dryRun = false)
    {
        using StreamReader body = new StreamReader(Request.Body);
        string text = await body.ReadToEndAsync();
        Result<Receipt> result = await _ingestion.IngestAsync(text, dryRun);
        if (!result.Succeeded)
        {
            return ErrorMapper.ToResult(result.Problem!);
        }
        return Ok(ReceiptMapper.ToDto(result.Value!));
    }
}
