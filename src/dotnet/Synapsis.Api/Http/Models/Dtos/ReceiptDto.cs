using System.Collections.Generic;

namespace Synapsis.Api.Http.Models.Dtos;

/// <summary>The answer to POST /ingest, as the feeders and the conformance suite read it.</summary>
public sealed class ReceiptDto
{
    /// <summary>The batch id, as sent.</summary>
    public required string BatchId { get; init; }

    /// <summary>"accepted", "partial", "rejected" or "unchanged".</summary>
    public required string Status { get; init; }

    /// <summary>The producer, as sent.</summary>
    public required string Producer { get; init; }

    /// <summary>When the server received the batch, as a SIL instant.</summary>
    public required string ReceivedAt { get; init; }

    /// <summary>The counts.</summary>
    public required CountsDto Counts { get; init; }

    /// <summary>The refused records, in batch order.</summary>
    public required List<RejectionDto> Rejected { get; init; }

    /// <summary>True when nothing was stored on purpose.</summary>
    public required bool DryRun { get; init; }
}
