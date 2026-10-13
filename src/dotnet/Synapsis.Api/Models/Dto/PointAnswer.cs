using System;
using System.Collections.Generic;

namespace Synapsis.Api.Models.Dto;

/// <summary>
/// The answer of GET /analyze/point: a clicked event or entity in full (the record as each source stated it, with
/// roles, attributes, measures and evidence) and its connections grouped by kind. With no connections, Connections is
/// empty and NoConnectionsReason says why.
/// </summary>
public sealed class PointAnswer
{
    /// <summary>The as_of used (now when none was given).</summary>
    public DateTime AsOf { get; init; }

    /// <summary>The point as a summary (label, type, time, place, participants).</summary>
    public required RecordSummary Summary { get; init; }

    /// <summary>Each source's latest state known at as_of, the record exactly as it arrived (as GET /records/{id}).</summary>
    public List<RecordAnswer> Statements { get; init; } = new List<RecordAnswer>();

    /// <summary>The connections, one group per kind and type.</summary>
    public List<ConnectionGroup> Connections { get; init; } = new List<ConnectionGroup>();

    /// <summary>Why there are no connections, such as "No entity takes part in this event."; null when there are some.</summary>
    public string? NoConnectionsReason { get; init; }
}
