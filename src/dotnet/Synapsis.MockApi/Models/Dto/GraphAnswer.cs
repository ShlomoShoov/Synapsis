using System;
using System.Collections.Generic;

namespace Synapsis.MockApi.Models.Dto;

/// <summary>
/// The answer of GET /analyze/graph: the clicked event or entity as a short summary, and its connections grouped by
/// kind. With no connections, Connections is empty and NoConnectionsReason says why. The full record (each source's
/// statement) is not here: the UI asks GET /records/{id} when the user wants the details.
/// </summary>
public sealed class GraphAnswer
{
    /// <summary>The as_of used (now when none was given).</summary>
    public DateTime AsOf { get; init; }

    /// <summary>The centre: the clicked record as a summary (label, type, time, place, participants).</summary>
    public required RecordSummary Center { get; init; }

    /// <summary>The connections, one group per kind and type.</summary>
    public List<ConnectionGroup> Connections { get; init; } = new List<ConnectionGroup>();

    /// <summary>Why there are no connections, such as "No entity takes part in this event."; null when there are some.</summary>
    public string? NoConnectionsReason { get; init; }
}
