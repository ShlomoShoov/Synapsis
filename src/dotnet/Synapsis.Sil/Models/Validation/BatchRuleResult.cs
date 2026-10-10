using System.Collections.Generic;
namespace Synapsis.Sil.Models.Validation;

/// <summary>
/// The answer of a batch rule: the records that pass on to the next rule, the ones it refused, and how many it dropped
/// as identical copies (dropped records are neither refused nor stored).
/// </summary>
public sealed class BatchRuleResult
{
    /// <summary>Creates a result.</summary>
    public BatchRuleResult(IReadOnlyList<CheckedRecord> passed, IReadOnlyList<Rejection> rejected, int dropped)
    {
        Passed = passed;
        Rejected = rejected;
        Dropped = dropped;
    }

    /// <summary>The records that pass on, in batch order.</summary>
    public IReadOnlyList<CheckedRecord> Passed { get; }

    /// <summary>The records this rule refused.</summary>
    public IReadOnlyList<Rejection> Rejected { get; }

    /// <summary>How many identical copies this rule dropped.</summary>
    public int Dropped { get; }
}
