using System.Collections.Generic;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// The outcome of checking a batch: the records accepted and the records refused, both in batch order, and how many
/// records were identical copies of an earlier one (counted, not stored, not refused).
/// </summary>
public sealed class BatchResult
{
    /// <summary>Creates a result.</summary>
    public BatchResult(List<PreparedRecord> accepted, List<Rejection> rejected, int duplicates)
    {
        Accepted = accepted;
        Rejected = rejected;
        Duplicates = duplicates;
    }

    /// <summary>The records that passed every rule, in batch order.</summary>
    public List<PreparedRecord> Accepted { get; }

    /// <summary>The refused records with their errors, in batch order.</summary>
    public List<Rejection> Rejected { get; }

    /// <summary>How many records were identical copies of an earlier record in the batch.</summary>
    public int Duplicates { get; }
}
