using System.Collections.Generic;
using Synapsis.Sil.Services.Registry;
namespace Synapsis.Sil.Models.Validation;

/// <summary>What a batch rule checks: the records still in the running, and the vocabulary.</summary>
public sealed class BatchRuleRequest
{
    /// <summary>Creates a request.</summary>
    public BatchRuleRequest(IReadOnlyList<CheckedRecord> records, IRegistry registry)
    {
        Records = records;
        Registry = registry;
    }

    /// <summary>The records that passed every earlier rule, in batch order.</summary>
    public IReadOnlyList<CheckedRecord> Records { get; }

    /// <summary>The vocabulary.</summary>
    public IRegistry Registry { get; }
}
