using System.Collections.Generic;

namespace Synapsis.Api.Models.Results;

/// <summary>One raw graph link row before MongoDB confirms and summarizes the connected record.</summary>
public sealed class GraphConnectionCandidate
{
    /// <summary>The link group kind: relation, event, or together.</summary>
    public required string Kind { get; init; }

    /// <summary>The connected record id, whether event or entity.</summary>
    public required string NeighborId { get; init; }

    /// <summary>The persisted id of a direct relation.</summary>
    public string? RelationId { get; init; }

    /// <summary>The direct relation type.</summary>
    public string? RelationType { get; init; }

    /// <summary>The relation direction from the center.</summary>
    public string? Direction { get; init; }

    /// <summary>A role for a participant candidate from an event record.</summary>
    public string? Role { get; init; }

    /// <summary>Distinct participation roles on an entity-to-event graph link.</summary>
    public List<string> Roles { get; init; } = new List<string>();

    /// <summary>The event id for a candidate that shares an event.</summary>
    public string? EventId { get; init; }

    /// <summary>Distinct event ids shared with an entity candidate.</summary>
    public List<string> EventIds { get; init; } = new List<string>();
}
