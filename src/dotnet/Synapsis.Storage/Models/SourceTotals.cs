namespace Synapsis.Storage.Models;

/// <summary>What one source sent over a period, summed from its ingestion log entries.</summary>
public sealed class SourceTotals
{
    /// <summary>The source, such as "src:cisa:kev".</summary>
    public required string Source { get; init; }

    /// <summary>How many batches brought records of this source.</summary>
    public long Batches { get; init; }

    /// <summary>Records received.</summary>
    public long Received { get; init; }

    /// <summary>Records stored for the first time.</summary>
    public long New { get; init; }

    /// <summary>Records stored as a new version.</summary>
    public long Changed { get; init; }

    /// <summary>Records already stored as they were.</summary>
    public long Unchanged { get; init; }

    /// <summary>Exact copies inside a batch, dropped.</summary>
    public long Duplicates { get; init; }

    /// <summary>Records refused.</summary>
    public long Rejected { get; init; }
}
