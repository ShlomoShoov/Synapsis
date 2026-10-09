using System.Collections.Generic;
using Synapsis.Sil.Vocabulary.Attributes;

namespace Synapsis.Sil.Vocabulary.Types;

/// <summary>
/// A relation type, for example PRODUCT_OF, and which entity types it may link.
/// </summary>
public sealed class RelationTypeDefinition
{
    /// <summary>The type code in capitals, for example "PRODUCT_OF".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The entity types the link may start from.</summary>
    public List<string> From { get; init; } = new List<string>();

    /// <summary>The entity types the link may point to.</summary>
    public List<string> To { get; init; } = new List<string>();

    /// <summary>The attributes records of this type may carry, by name.</summary>
    public Dictionary<string, AttributeDefinition> Attributes { get; init; } = new Dictionary<string, AttributeDefinition>();
}
