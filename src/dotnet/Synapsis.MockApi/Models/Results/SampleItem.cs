using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Entities;

namespace Synapsis.MockApi.Models.Results;

/// <summary>
/// One record of the sample as known at an as_of: its summary (what the answers show), the words keyword search reads,
/// the record of its most reliable statement (for participants and roles) and every statement (for the point's details).
/// </summary>
public sealed class SampleItem
{
    /// <summary>What the answers show.</summary>
    public required RecordSummary Summary { get; init; }

    /// <summary>The words of the name, title and body, in lower case.</summary>
    public HashSet<string> Words { get; init; } = new HashSet<string>();

    /// <summary>The record of the most reliable statement.</summary>
    public required SilRecord Record { get; init; }

    /// <summary>Each source's latest state known at the as_of.</summary>
    public List<SampleRecord> Statements { get; init; } = new List<SampleRecord>();
}
