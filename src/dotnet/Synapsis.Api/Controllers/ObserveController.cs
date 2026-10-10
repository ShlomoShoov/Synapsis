using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Time;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories;

namespace Synapsis.Api.Controllers;

/// <summary>
/// GET /observe/…: the ingestion log, for the UI's live view. The log entries are returned as stored (see
/// <see cref="IngestLogEntry"/>): they are already the shape the UI reads, so no separate DTO is kept.
/// </summary>
[ApiController]
public sealed class ObserveController : ControllerBase
{
    /// <summary>How many entries GET /observe/ingest returns when no limit is asked.</summary>
    private const int DefaultLimit = 100;

    /// <summary>The most entries one call may ask for.</summary>
    private const int MaxLimit = 1000;

    private readonly IIngestLogRepository _log;
    private readonly TimeProvider _clock;

    /// <summary>Creates the controller over the ingestion log.</summary>
    public ObserveController(IIngestLogRepository log, TimeProvider clock)
    {
        _log = log;
        _clock = clock;
    }

    /// <summary>
    /// 200 with the newest log entries, newest first. since (a UTC instant) returns only later entries: the UI polls
    /// with the timestamp of the newest entry it has. source keeps one source; limit is 1 to 1000 (default 100).
    /// </summary>
    [HttpGet("observe/ingest")]
    public async Task<IActionResult> Ingest([FromQuery(Name = "since")] string? since, [FromQuery(Name = "source")] string? source, [FromQuery(Name = "limit")] int? limit)
    {
        Result<DateTime?> after = ReadInstant("since", since);
        if (!after.Succeeded)
        {
            return ErrorMapper.ToResult(after.Problem!);
        }
        int size = limit ?? DefaultLimit;
        if (size < 1 || size > MaxLimit)
        {
            return ErrorMapper.ToResult(new Problem(ErrorCodes.Param, "limit", $"limit must be between 1 and {MaxLimit}"));
        }
        IReadOnlyList<IngestLogEntry> entries = await _log.LatestAsync(after.Value, source, size);
        return Ok(entries);
    }

    /// <summary>200 with the totals between from and to (UTC instants; default: the last 24 hours): per source, and refusals per error code.</summary>
    [HttpGet("observe/stats")]
    public async Task<IActionResult> Stats([FromQuery(Name = "from")] string? from, [FromQuery(Name = "to")] string? to)
    {
        Result<DateTime?> start = ReadInstant("from", from);
        Result<DateTime?> end = ReadInstant("to", to);
        if (!start.Succeeded)
        {
            return ErrorMapper.ToResult(start.Problem!);
        }
        if (!end.Succeeded)
        {
            return ErrorMapper.ToResult(end.Problem!);
        }
        DateTime until = end.Value ?? _clock.GetUtcNow().UtcDateTime;
        DateTime since = start.Value ?? until.AddDays(-1);
        IngestStats stats = await _log.StatsAsync(since, until);
        return Ok(stats);
    }

    /// <summary>An optional query parameter holding a UTC instant ("2026-10-10T08:00:00Z"); anything else is E-PARAM.</summary>
    private static Result<DateTime?> ReadInstant(string name, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Result<DateTime?>.Success(null);
        }
        if (Instant.TryParse(text, out DateTime instant))
        {
            return Result<DateTime?>.Success(instant);
        }
        return Result<DateTime?>.Failure(ErrorCodes.Param, name, $"{name} must be a UTC instant (2026-10-10T08:00:00Z)");
    }
}
