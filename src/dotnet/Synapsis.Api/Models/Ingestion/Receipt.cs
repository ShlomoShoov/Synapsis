using System;
using System.Collections.Generic;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Api.Models.Ingestion;

/// <summary>The answer to a batch: what happened to its records, and why the refused ones were refused.</summary>
public sealed class Receipt
{
    /// <summary>The batch id, as sent.</summary>
    public required string BatchId { get; init; }

    /// <summary>The batch's outcome in one word.</summary>
    public required ReceiptStatus Status { get; init; }

    /// <summary>The producer, as sent.</summary>
    public required string Producer { get; init; }

    /// <summary>When the server received the batch (UTC).</summary>
    public required DateTime ReceivedAt { get; init; }

    /// <summary>The counts.</summary>
    public required ReceiptCounts Counts { get; init; }

    /// <summary>The refused records with their problems, in batch order.</summary>
    public required IReadOnlyList<Rejection> Rejected { get; init; }

    /// <summary>True when nothing was stored on purpose (a dry run).</summary>
    public required bool DryRun { get; init; }
}
