using System.Collections.Generic;
using Synapsis.Sil.Models.Vocabulary.Attributes;
namespace Synapsis.Sil.Models.Vocabulary.Types;

/// <summary>
/// An entity type, for example Bank or Vulnerability, and the attributes its records may carry.
/// </summary>
public sealed class EntityTypeDefinition
{
    /// <summary>The type code, starting with a capital letter, for example "Bank".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The attributes records of this type may carry, by name.</summary>
    public Dictionary<string, AttributeDefinition> Attributes { get; init; } = new Dictionary<string, AttributeDefinition>();
}
