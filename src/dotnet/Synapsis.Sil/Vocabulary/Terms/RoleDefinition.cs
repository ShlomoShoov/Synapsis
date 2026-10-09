namespace Synapsis.Sil.Vocabulary.Terms;

/// <summary>
/// A role an entity can play in an event, for example subject, actor or vendor.
/// </summary>
public sealed class RoleDefinition
{
    /// <summary>The role code in lower case, for example "affected_product".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;
}
