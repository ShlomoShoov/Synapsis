using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Entities;
using Synapsis.MockApi.Repositories;

namespace Synapsis.MockApi.Controllers;

/// <summary>GET /records/{id}: a record as each source stated it, as known at a date (the full details), as the real API answers it.</summary>
[ApiController]
public sealed class RecordsController : ControllerBase
{
    private readonly SampleRepository _sampleRepository;

    /// <summary>Creates the controller over the sample.</summary>
    public RecordsController(SampleRepository sampleRepository)
    {
        _sampleRepository = sampleRepository;
    }

    /// <summary>
    /// 200 with each source's latest state known at as_of; 404 when there is none; 400 for an as_of that is not a date.
    /// as_of: a date ("2024-05-01" = the start of that day, UTC) or a date-time ("2024-05-01T12:00:00Z"); none = now.
    /// </summary>
    [HttpGet("records/{**id}")]
    public IActionResult Get(string id, [FromQuery(Name = "as_of")] string? asOf, [FromQuery(Name = "source")] string? source)
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
        List<SampleRecord> found = _sampleRepository.StatementsOf(id, moment, source);
        if (found.Count == 0)
        {
            return Problem(statusCode: 404, detail: "no record '" + id + "' known at " + (asOf ?? "now"));
        }

        // 3. Answer them as JSON.
        List<RecordAnswer> answers = new List<RecordAnswer>();
        foreach (SampleRecord sampleRecord in found)
        {
            answers.Add(new RecordAnswer
            {
                Source = sampleRecord.Source, State = sampleRecord.State, KnownAt = sampleRecord.KnownAt, ReceivedAt = sampleRecord.ReceivedAt,
                BatchId = sampleRecord.BatchId, Producer = sampleRecord.Producer, Record = sampleRecord.Record,
            });
        }
        return Ok(answers);
    }
}
