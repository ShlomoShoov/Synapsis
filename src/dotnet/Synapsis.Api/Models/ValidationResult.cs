using System.Collections.Generic;
using Synapsis.Api.Models.Records;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Models;

/// <summary>The answer of the validation step: the valid records, the refused ones, and the exact copies dropped.</summary>
public sealed class ValidationResult
{
    /// <summary>The records that passed, in batch order.</summary>
    public List<SilRecord> Valid { get; set; } = new List<SilRecord>();

    /// <summary>The records refused, each with its code and reason.</summary>
    public List<RejectedRecord> Rejected { get; set; } = new List<RejectedRecord>();

    /// <summary>Exact copies of an earlier record in the same batch (same id, source and content): dropped.</summary>
    public List<SilRecord> Duplicates { get; set; } = new List<SilRecord>();
}
