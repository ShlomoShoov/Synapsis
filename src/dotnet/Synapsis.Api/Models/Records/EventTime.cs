using System;

namespace Synapsis.Api.Models.Records;

/// <summary>When an event happened. Only the start is read; the rest (end, precision) is stored as it arrived.</summary>
public sealed class EventTime
{
    /// <summary>When it started (required). Not a date-time: the record cannot be read.</summary>
    public DateTimeOffset? Start { get; set; }
}
