using System.Collections.Generic;

namespace Synapsis.Sil.Models.Domain.Vocabulary.Terms;

/// <summary>
/// A logical space: a tree of positions that are not on a map, such as vendor then product. A closed space only
/// accepts its registered values at the top level (rule A-CLOSED, which bounds what a model may write).
/// </summary>
public sealed class LogicalSpaceDefinition
{
    /// <summary>The space's name, for example "cyber.product_class".</summary>
    public string Space { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The longest path allowed, from 1 to 8 levels.</summary>
    public int MaxDepth { get; init; }

    /// <summary>True when the top level only accepts the registered Values. False when the package does not say.</summary>
    public bool Closed { get; init; }

    /// <summary>The values a closed space accepts at its top level.</summary>
    public List<string> Values { get; init; } = new List<string>();
}
