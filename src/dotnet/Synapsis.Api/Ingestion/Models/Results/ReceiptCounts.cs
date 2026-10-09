namespace Synapsis.Api.Ingestion.Models.Results;

/// <summary>How a batch's records were counted. Received = new + versioned + unchanged + duplicates + rejected.</summary>
public sealed class ReceiptCounts
{
    /// <summary>Records in the batch.</summary>
    public required int Received { get; init; }

    /// <summary>Accepted records that started a new statement (version 1).</summary>
    public required int New { get; init; }

    /// <summary>Accepted records stored as the next version of their statement.</summary>
    public required int Versioned { get; init; }

    /// <summary>Accepted records the store already held.</summary>
    public required int Unchanged { get; init; }

    /// <summary>Identical copies of an earlier record in the same batch.</summary>
    public required int Duplicates { get; init; }

    /// <summary>Refused records.</summary>
    public required int Rejected { get; init; }
}
