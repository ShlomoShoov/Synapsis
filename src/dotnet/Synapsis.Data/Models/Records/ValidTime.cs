using System;

namespace Synapsis.Data.Models.Records;

/// <summary>When something happened: a start, its precision, and an end when it lasted.</summary>
public sealed class ValidTime
{
    /// <summary>When it started.</summary>
    public DateTimeOffset? Start { get; set; }

    /// <summary>How precise the start is: second, minute, hour, day, week, month, quarter or year.</summary>
    public string? Precision { get; set; }

    /// <summary>When it ended, when it lasted.</summary>
    public DateTimeOffset? End { get; set; }
}
