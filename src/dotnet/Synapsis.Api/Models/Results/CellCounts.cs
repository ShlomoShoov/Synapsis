using System.Collections.Generic;

namespace Synapsis.Api.Models.Results;

/// <summary>What Elasticsearch counts for the map's cells layer: the matching events per H3 cell, counted by record id.</summary>
public sealed class CellCounts
{
    /// <summary>Matching events that have a cell at this resolution (distinct record ids, approximate).</summary>
    public int Events { get; init; }

    /// <summary>How many cells hold matching events (approximate).</summary>
    public int TotalCells { get; init; }

    /// <summary>The cells returned (at most the limit, the largest first) with their event counts.</summary>
    public Dictionary<string, int> CountByCell { get; init; } = new Dictionary<string, int>();
}
