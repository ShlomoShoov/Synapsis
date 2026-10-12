using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Models.Results;
using Synapsis.Data.Models.Entities;
using Synapsis.Api.Repositories;
using Synapsis.Data.Services;

namespace Synapsis.Api.Controllers;

/// <summary>GET /records/{id}: a record as each source stated it, as known at a date.</summary>
[ApiController]
public sealed class RecordsController : ControllerBase
{
    private readonly RecordRepository _recordRepository;

    /// <summary>Creates the controller over the record store.</summary>
    public RecordsController(RecordRepository recordRepository)
    {
        _recordRepository = recordRepository;
    }

    /// <summary>
    /// 200 with each source's latest state known at as_of; 404 when there is none; 400 for an as_of that is not a date.
    /// as_of: a date ("2024-05-01" = the start of that day, UTC) or a date-time ("2024-05-01T12:00:00Z"); none = now.
    /// </summary>
    [HttpGet("records/{**id}")]
    public async Task<IActionResult> Get(string id, [FromQuery(Name = "as_of")] string? asOf, [FromQuery(Name = "source")] string? source)
    {
        // 1. Read as_of (a date alone is midnight UTC, the start of that day).
        DateTime? moment = null;
        if (!string.IsNullOrEmpty(asOf))
        {
            DateTimeStyles utc = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            if (!DateTime.TryParse(asOf, CultureInfo.InvariantCulture, utc, out DateTime parsed))
            {
                return Problem(statusCode: 400, detail: "as_of must be a date (2024-05-01) or a date-time (2024-05-01T12:00:00Z)");
            }
            moment = parsed;
        }

        // 2. Find each source's latest state known at that moment.
        List<StoredRecord> found = await _recordRepository.AsOfAsync(id, moment, source);
        if (found.Count == 0)
        {
            return Problem(statusCode: 404, detail: "no record '" + id + "' known at " + (asOf ?? "now"));
        }

        // 3. Answer them as JSON.
        List<RecordAnswer> answers = new List<RecordAnswer>();
        foreach (StoredRecord stored in found)
        {
            answers.Add(ToAnswer(stored));
        }
        return Ok(answers);
    }

    /// <summary>The stored record as an answer: its record turned from BSON back into plain JSON.</summary>
    private static RecordAnswer ToAnswer(StoredRecord stored)
    {
        RecordAnswer answer = new RecordAnswer();
        answer.Source = stored.Source;
        answer.State = stored.State;
        answer.KnownAt = stored.KnownAt;
        answer.ReceivedAt = stored.ReceivedAt;
        answer.BatchId = stored.BatchId;
        answer.Producer = stored.Producer;
        answer.Record = RecordReader.ToJson(stored.Record);
        return answer;
    }
}
