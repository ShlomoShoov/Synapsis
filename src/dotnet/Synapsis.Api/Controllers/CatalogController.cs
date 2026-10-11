using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories;

namespace Synapsis.Api.Controllers;

/// <summary>GET /catalog: every source and type that has entered the system, for the UI's lists and filters.</summary>
[ApiController]
public sealed class CatalogController : ControllerBase
{
    private readonly CatalogRepository _catalogRepository;

    /// <summary>Creates the controller over the catalog.</summary>
    public CatalogController(CatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    /// <summary>200 with every catalog entry, sorted by field, then value.</summary>
    [HttpGet("catalog")]
    public async Task<List<CatalogEntry>> Get()
    {
        return await _catalogRepository.AllAsync();
    }
}
