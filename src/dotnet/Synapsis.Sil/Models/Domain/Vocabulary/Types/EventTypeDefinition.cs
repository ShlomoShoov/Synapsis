using System.Collections.Generic;
using Synapsis.Sil.Models.Domain.Vocabulary.Attributes;

namespace Synapsis.Sil.Models.Domain.Vocabulary.Types;

/// <summary>
/// An event type, for example CYB.VULN.KNOWN_EXPLOITED: which place forms, roles, measures, text and attributes
/// its records may use.
/// </summary>
public sealed class EventTypeDefinition
{
    /// <summary>The type code: a domain then dot-separated parts in capitals, for example "SEC.CONFLICT.STATE_BASED".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The place forms a record may use; at least one of them must be present on each record.</summary>
    public List<PlaceForm> Place { get; init; } = new List<PlaceForm>();

    /// <summary>The roles allowed in the event, each with the entity types that may fill it (at least one).</summary>
    public Dictionary<string, List<string>> Roles { get; init; } = new Dictionary<string, List<string>>();

    /// <summary>The measures a record may carry, by name.</summary>
    public List<string> Measures { get; init; } = new List<string>();

    /// <summary>Whether records carry text; none when the package does not say.</summary>
    public TextPolicy Text { get; init; } = TextPolicy.None;

    /// <summary>The attributes records of this type may carry, by name.</summary>
    public Dictionary<string, AttributeDefinition> Attributes { get; init; } = new Dictionary<string, AttributeDefinition>();
}
