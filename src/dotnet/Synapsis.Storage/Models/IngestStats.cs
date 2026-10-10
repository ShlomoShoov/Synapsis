using System;
using System.Collections.Generic;

namespace Synapsis.Storage.Models;

/// <summary>Totals of the ingestion log over a period: per source, and refusals per error code.</summary>
public sealed class IngestStats
{
    /// <summary>The start of the period (UTC).</summary>
    public required DateTime From { get; init; }

    /// <summary>The end of the period (UTC).</summary>
    public required DateTime To { get; init; }

    /// <summary>The totals of each source that sent something in the period, busiest first.</summary>
    public List<SourceTotals> Sources { get; init; } = new List<SourceTotals>();

    /// <summary>How many records each error code refused in the period, most frequent first.</summary>
    public List<RejectionCount> Codes { get; init; } = new List<RejectionCount>();
}
