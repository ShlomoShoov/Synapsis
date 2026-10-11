using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Models;
using Synapsis.Api.Orchestrators;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Controllers;

/// <summary>POST /ingest: one batch of records (JSON, gzip allowed).</summary>
[ApiController]
public sealed class IngestController : ControllerBase
{
    private readonly IngestionOrchestrator _ingestionOrchestrator;

    /// <summary>Creates the controller over the ingestion process.</summary>
    public IngestController(IngestionOrchestrator ingestionOrchestrator)
    {
        _ingestionOrchestrator = ingestionOrchestrator;
    }

    /// <summary>200 with the batch summary. A body that is not a batch: 400, answered by ASP.NET before this runs.</summary>
    [HttpPost("ingest")]
    public async Task<IngestSummary> Post([FromBody] IngestBatch batch)
    {
        return await _ingestionOrchestrator.IngestAsync(batch);
    }
}
