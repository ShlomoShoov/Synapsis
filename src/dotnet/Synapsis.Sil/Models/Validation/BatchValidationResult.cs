using System.Collections.Generic;
namespace Synapsis.Sil.Models.Validation;

/// <summary>
/// The outcome of validating a batch: the records accepted and the records refused, both in batch order, and how
/// many records were identical copies of an earlier one (counted, not refused, not stored).
/// </summary>
public sealed class BatchValidationResult
{
    /// <summary>Creates a result.</summary>
    public BatchValidationResult(IReadOnlyList<CheckedRecord> accepted, IReadOnlyList<Rejection> rejected, int duplicates)
    {
        Accepted = accepted;
        Rejected = rejected;
        Duplicates = duplicates;
    }

    /// <summary>The records that passed every rule, in batch order.</summary>
    public IReadOnlyList<CheckedRecord> Accepted { get; }

    /// <summary>The refused records with their problems, in batch order.</summary>
    public IReadOnlyList<Rejection> Rejected { get; }

    /// <summary>How many records were identical copies of an earlier record in the batch.</summary>
    public int Duplicates { get; }
}
