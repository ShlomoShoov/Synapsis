using System.Collections.Generic;
using Synapsis.Api.Http.Models.Dtos;
using Synapsis.Api.Ingestion.Models.Results;
using Synapsis.Sil.Services.Time;
using Synapsis.Sil.Validation.Models.Results;

namespace Synapsis.Api.Http.Services.Mappers;

/// <summary>Turns a receipt into its HTTP body.</summary>
public static class ReceiptMapper
{
    /// <summary>The receipt as the answer to POST /ingest.</summary>
    public static ReceiptDto ToDto(Receipt receipt)
    {
        List<RejectionDto> rejected = new List<RejectionDto>();
        foreach (Rejection rejection in receipt.Rejected)
        {
            rejected.Add(ToDto(rejection));
        }
        return new ReceiptDto
        {
            BatchId = receipt.BatchId,
            Status = receipt.Status.ToString().ToLowerInvariant(),
            Producer = receipt.Producer,
            ReceivedAt = Instant.Format(receipt.ReceivedAt),
            Counts = ToDto(receipt.Counts),
            Rejected = rejected,
            DryRun = receipt.DryRun,
        };
    }

    /// <summary>The counts.</summary>
    private static CountsDto ToDto(ReceiptCounts counts)
    {
        return new CountsDto
        {
            Received = counts.Received,
            New = counts.New,
            Versioned = counts.Versioned,
            Unchanged = counts.Unchanged,
            Duplicates = counts.Duplicates,
            Rejected = counts.Rejected,
        };
    }

    /// <summary>One refused record.</summary>
    private static RejectionDto ToDto(Rejection rejection)
    {
        return new RejectionDto
        {
            Index = rejection.Index,
            Id = rejection.Id,
            Code = rejection.Problem.Code,
            Path = rejection.Problem.Path,
            Message = rejection.Problem.Message,
        };
    }
}
