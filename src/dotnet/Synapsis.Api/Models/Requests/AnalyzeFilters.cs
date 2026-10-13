using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Services;

namespace Synapsis.Api.Models.Requests;

/// <summary>
/// The filters every Analyze route accepts (docs/phase-2-read-api.md §4.1), read by ASP.NET from the query string.
/// A list filter is a repeated parameter (type=A&amp;type=B): any of its values matches; different filters must all
/// match. A missing value or an empty list means "no filter". ASP.NET answers 400 when a value cannot be read (a date
/// that is not a date) or when Validate finds a mistake. Each route extends it with its own parameters (MapRequest,
/// GraphRequest), so the class is not sealed.
/// </summary>
public class AnalyzeFilters : IValidatableObject
{
    /// <summary>What was known by then (known_at not after it). A date alone is the start of that day, UTC. None = now.</summary>
    [FromQuery(Name = "as_of")]
    public DateTime? AsOf { get; set; }

    /// <summary>Keywords: every word must appear in the name, title or body (or the whole text is the id).</summary>
    [FromQuery(Name = "q")]
    public string? Keywords { get; set; }

    /// <summary>Record kinds to keep: entity, event, conclusion.</summary>
    [FromQuery(Name = "kind")]
    public List<string> Kinds { get; set; } = new List<string>();

    /// <summary>Record types to keep, such as "SEC.ATTACK.BOMBING".</summary>
    [FromQuery(Name = "type")]
    public List<string> Types { get; set; } = new List<string>();

    /// <summary>Sources to keep, such as "src:start:gtd".</summary>
    [FromQuery(Name = "source")]
    public List<string> Sources { get; set; } = new List<string>();

    /// <summary>Events that start at or after this moment.</summary>
    [FromQuery(Name = "from")]
    public DateTime? From { get; set; }

    /// <summary>Events that start before this moment (not at it).</summary>
    [FromQuery(Name = "to")]
    public DateTime? To { get; set; }

    /// <summary>The map's box, "min_lon,min_lat,max_lon,max_lat": events whose point is inside.</summary>
    [FromQuery(Name = "bbox")]
    public string? BoundingBox { get; set; }

    /// <summary>H3 cells (resolution 2 to 5): events whose point is in one of them.</summary>
    [FromQuery(Name = "cell")]
    public List<string> Cells { get; set; } = new List<string>();

    /// <summary>Country codes (ISO 3166-1 alpha-2), such as "US".</summary>
    [FromQuery(Name = "country")]
    public List<string> Countries { get; set; } = new List<string>();

    /// <summary>Subdivision codes (ISO 3166-2), such as "US-NV".</summary>
    [FromQuery(Name = "subdivision")]
    public List<string> Subdivisions { get; set; } = new List<string>();

    /// <summary>Logical nodes, such as "cyber.vendor_product/Apache": records at or below one of them.</summary>
    [FromQuery(Name = "logical")]
    public List<string> LogicalNodes { get; set; } = new List<string>();

    /// <summary>Entity ids: events in which one of them takes part.</summary>
    [FromQuery(Name = "entity")]
    public List<string> Entities { get; set; } = new List<string>();

    /// <summary>Measure conditions "name:op:value" (op gt, gte, lt, lte or eq), such as "magnitude:gte:5"; all must hold.</summary>
    [FromQuery(Name = "measure")]
    public List<string> Measures { get; set; } = new List<string>();

    /// <summary>Called by ASP.NET after reading the values: every mistake, each naming its parameter (400 when any).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return FilterParser.Check(this);
    }
}
