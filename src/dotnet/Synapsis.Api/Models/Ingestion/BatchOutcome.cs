using Synapsis.Sil.Models.Versioning;
using System;
using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Api.Models.Ingestion;

/// <summary>What happened to one batch: the batch, its validation, the plans of its accepted records. The receipt and the ingestion log are both built from it.</summary>
public sealed class BatchOutcome
{
    /// <summary>Creates the outcome of one batch.</summary>
    public BatchOutcome(IngestBundle bundle, BatchValidationResult validation, IReadOnlyList<PlannedRecord> planned, DateTime receivedAt, bool dryRun)
    {
        Bundle = bundle;
        Validation = validation;
        Planned = planned;
        ReceivedAt = receivedAt;
        DryRun = dryRun;
    }

    /// <summary>The batch.</summary>
    public IngestBundle Bundle { get; }

    /// <summary>Its validation: accepted, refused, duplicates.</summary>
    public BatchValidationResult Validation { get; }

    /// <summary>Every accepted record with its version plan.</summary>
    public IReadOnlyList<PlannedRecord> Planned { get; }

    /// <summary>When the server received the batch (UTC).</summary>
    public DateTime ReceivedAt { get; }

    /// <summary>True for a dry run.</summary>
    public bool DryRun { get; }
}
