using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Synapsis.MockApi.Models.Requests;

/// <summary>
/// The parameters of GET /analyze/map: the shared filters (bbox is the map's box, from and to the time window) and
/// the map's own: its zoom, which layer to draw, and paging.
/// </summary>
public sealed class MapRequest : AnalyzeFilters
{
    /// <summary>The map's zoom (0 to 22). It chooses the H3 cell size: 0-3 resolution 2, 4-5 resolution 3, 6-7 resolution 4, 8 and more resolution 5.</summary>
    [FromQuery(Name = "zoom")]
    [Required(ErrorMessage = "zoom is required (the map's zoom, 0 to 22).")]
    [Range(0, 22, ErrorMessage = "zoom must be between 0 and 22.")]
    public double? Zoom { get; set; }

    /// <summary>The layer to draw: points (each event) or cells (H3 hexagons with counts). Required.</summary>
    [FromQuery(Name = "layer")]
    [Required(ErrorMessage = "layer is required (points or cells).")]
    [RegularExpression("^(points|cells)$", ErrorMessage = "layer must be points or cells.")]
    public string? Layer { get; set; }

    /// <summary>The page size: points or cells per answer.</summary>
    [FromQuery(Name = "limit")]
    [Range(1, 5000, ErrorMessage = "limit must be between 1 and 5000.")]
    public int Limit { get; set; } = 2000;

    /// <summary>The "next" of the previous page, to read the following one.</summary>
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; set; }
}
