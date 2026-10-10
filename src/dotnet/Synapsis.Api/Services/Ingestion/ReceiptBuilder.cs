using Synapsis.Api.Models.Ingestion;
using Synapsis.Sil.Models.Versioning;
namespace Synapsis.Api.Services.Ingestion;

/// <summary>
/// Counts each record once, by what happened to it, and decides the status: rejected when every record was refused;
/// partial when some were; unchanged when nothing new was stored; accepted otherwise.
/// </summary>
public sealed class ReceiptBuilder : IReceiptBuilder
{
    /// <inheritdoc/>
    public Receipt Build(BatchOutcome outcome)
    {
        ReceiptCounts counts = Count(outcome);
        return new Receipt
        {
            BatchId = outcome.Bundle.BatchId,
            Status = StatusOf(counts),
            Producer = outcome.Bundle.Producer,
            ReceivedAt = outcome.ReceivedAt,
            Counts = counts,
            Rejected = outcome.Validation.Rejected,
            DryRun = outcome.DryRun,
        };
    }

    /// <summary>The counts of the batch.</summary>
    private static ReceiptCounts Count(BatchOutcome outcome)
    {
        int created = 0;
        int versioned = 0;
        int unchanged = 0;
        foreach (PlannedRecord planned in outcome.Planned)
        {
            if (planned.Plan.Outcome == VersionOutcome.New)
            {
                created++;
            }
            else if (planned.Plan.Outcome == VersionOutcome.NewVersion)
            {
                versioned++;
            }
            else
            {
                unchanged++;
            }
        }
        return new ReceiptCounts
        {
            Received = outcome.Bundle.Records.Count,
            New = created,
            Versioned = versioned,
            Unchanged = unchanged,
            Duplicates = outcome.Validation.Duplicates,
            Rejected = outcome.Validation.Rejected.Count,
        };
    }

    /// <summary>The batch's outcome in one word.</summary>
    private static ReceiptStatus StatusOf(ReceiptCounts counts)
    {
        if (counts.Rejected == counts.Received)
        {
            return ReceiptStatus.Rejected;
        }
        if (counts.Rejected > 0)
        {
            return ReceiptStatus.Partial;
        }
        if (counts.New + counts.Versioned == 0)
        {
            return ReceiptStatus.Unchanged;
        }
        return ReceiptStatus.Accepted;
    }
}
