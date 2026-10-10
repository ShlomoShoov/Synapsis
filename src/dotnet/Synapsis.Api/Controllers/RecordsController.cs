using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Api.Services.Http;
using Synapsis.Sil.Models.Results;
using Synapsis.Storage.Repositories;
using Synapsis.Storage.Models;
namespace Synapsis.Api.Controllers;

/// <summary>GET /records/{id}?as_of=&amp;source=: a record as each source stated it, as known at a date.</summary>
[ApiController]
public sealed class RecordsController : ControllerBase
{
    private readonly IRecordRepository _records;

    /// <summary>Creates the controller over the record store (a plain read needs no process).</summary>
    public RecordsController(IRecordRepository records)
    {
        _records = records;
    }

    /// <summary>200 with each source's latest version known by as_of; 404 when there is none; 400 for a bad as_of.</summary>
    [HttpGet("records/{**id}")]
    public async Task<IActionResult> Get(string id, [FromQuery(Name = "as_of")] string? asOf, [FromQuery(Name = "source")] string? source)
    {
        Result<DateTime?> moment = AsOfReader.Read(asOf);
        if (!moment.Succeeded)
        {
            return ErrorMapper.ToResult(moment.Problem!);
        }
        IReadOnlyList<StoredStatement> statements = await _records.AsOfAsync(new StatementsAsOfRequest(id, moment.Value, source));
        if (statements.Count == 0)
        {
            return ErrorMapper.ToResult(new Problem(ErrorCodes.NotFound, string.Empty, $"no record '{id}' known by {asOf ?? "now"}"));
        }
        return Ok(StatementsMapper.ToDto(id, asOf, statements));
    }
}
