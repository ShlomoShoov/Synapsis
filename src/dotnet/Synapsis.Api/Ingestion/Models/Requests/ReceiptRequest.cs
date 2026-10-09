using System;
using System.Collections.Generic;
using Synapsis.Api.Ingestion.Models.Domain;
using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Validation.Models.Results;

namespace Synapsis.Api.Ingestion.Models.Requests;

/// <summary>What a receipt summarises: the batch, its validation, the plans of its accepted records.</summary>
public sealed class ReceiptRequest
{
    /// <summary>Creates a request.</summary>
    public ReceiptRequest(IngestBundle bundle, BatchValidationResult validation, IReadOnlyList<PlannedRecord> planned, DateTime receivedAt, bool dryRun)
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
