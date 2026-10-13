using System;
using System.Collections.Generic;

namespace Synapsis.Api.Models.Dto;

/// <summary>
/// The answer of GET /analyze/map: the matching events inside the map's box, either as points (zoomed in: oldest
/// first) or as H3 cells with counts (zoomed out: largest first). Only the list of the chosen layer is filled. What
/// matched but cannot be drawn is counted in Omitted.
/// </summary>
public sealed class MapAnswer
{
    /// <summary>The as_of used (now when none was given).</summary>
    public DateTime AsOf { get; init; }

    /// <summary>points or cells (auto resolved).</summary>
    public required string Layer { get; init; }

    /// <summary>The H3 resolution of the cells; null for points.</summary>
    public int? Resolution { get; init; }

    /// <summary>The matching events drawn (in all the points or cells, not only this page).</summary>
    public int Events { get; init; }

    /// <summary>All the points or cells, not only this page.</summary>
    public int Total { get; init; }

    /// <summary>The cursor of the next page; null on the last page.</summary>
    public string? Next { get; init; }

    /// <summary>What matched but cannot be drawn, with the reason; empty when nothing.</summary>
    public List<OmittedCount> Omitted { get; init; } = new List<OmittedCount>();

    /// <summary>The events, when the layer is points.</summary>
    public List<RecordSummary> Points { get; init; } = new List<RecordSummary>();

    /// <summary>The hexagons, when the layer is cells.</summary>
    public List<MapCell> Cells { get; init; } = new List<MapCell>();
}
