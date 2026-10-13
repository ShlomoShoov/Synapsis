using System.Collections.Generic;

namespace Synapsis.MockApi.Models.Dto;

/// <summary>
/// The connections of a point of one kind and type, such as "the events it takes part in, of type
/// SEC.ATTACK.BOMBING". Kinds: relation (a direct relation between entities), together (entities that take part in the
/// same events), event (the events an entity takes part in), participant (the entities taking part in an event).
/// </summary>
public sealed class ConnectionGroup
{
    /// <summary>The group's key, such as "relation:PRODUCT_OF:out": send it back as group to read the next page.</summary>
    public required string Key { get; init; }

    /// <summary>relation, together, event or participant.</summary>
    public required string Kind { get; init; }

    /// <summary>The relation type, the event type, or the type of the entities.</summary>
    public string? Type { get; init; }

    /// <summary>What to show as the group's title.</summary>
    public required string Label { get; init; }

    /// <summary>All the connections in the group, not only this page.</summary>
    public int Count { get; init; }

    /// <summary>The cursor of the group's next page; null when this is the last.</summary>
    public string? Next { get; init; }

    /// <summary>This page of the group's connections.</summary>
    public List<ConnectedRecord> Items { get; init; } = new List<ConnectedRecord>();
}
