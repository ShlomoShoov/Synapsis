using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Synapsis.Api.Models.Requests;

/// <summary>
/// The parameters of GET /analyze/point: the id of the clicked event or entity, the shared filters (they keep the
/// events through which things connect), and paging of the connection groups.
/// </summary>
public sealed class PointRequest : AnalyzeFilters
{
    /// <summary>The record's id, such as "evt:gtd:197001060001" (in the query string, because ids may contain "/").</summary>
    [FromQuery(Name = "id")]
    [Required(ErrorMessage = "id is required.")]
    public string? Id { get; set; }

    /// <summary>One group's key (from a previous answer): answer only that group, the page after cursor.</summary>
    [FromQuery(Name = "group")]
    public string? Group { get; set; }

    /// <summary>Connections per group in one answer.</summary>
    [FromQuery(Name = "limit")]
    [Range(1, 200, ErrorMessage = "limit must be between 1 and 200.")]
    public int Limit { get; set; } = 20;

    /// <summary>The "next" of a group, with group set, to read its following page.</summary>
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; set; }
}
