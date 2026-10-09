namespace Synapsis.Api.Http.Models.Dtos;

/// <summary>A receipt's counts: received = new + versioned + unchanged + duplicates + rejected.</summary>
public sealed class CountsDto
{
    /// <summary>Records in the batch.</summary>
    public required int Received { get; init; }

    /// <summary>Stored as version 1 of a new statement.</summary>
    public required int New { get; init; }

    /// <summary>Stored as the next version of their statement.</summary>
    public required int Versioned { get; init; }

    /// <summary>Already held by the store.</summary>
    public required int Unchanged { get; init; }

    /// <summary>Identical copies of an earlier record in the batch.</summary>
    public required int Duplicates { get; init; }

    /// <summary>Refused.</summary>
    public required int Rejected { get; init; }
}
