namespace Synapsis.Sil.Packages;

/// <summary>
/// An authority namespace any package may create entities in (core only), with the entity type and the key format it
/// holds. Example: namespace cve, entity type Vulnerability, keys like CVE-2023-34362.
/// </summary>
public sealed class SharedNamespace
{
    /// <summary>The namespace, for example "cve".</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>The only entity type its records may have, for example "Vulnerability".</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>A regular expression every key must match, for example ^CVE-[0-9]{4}-[0-9]{4,7}$.</summary>
    public string KeyPattern { get; init; } = string.Empty;

    /// <summary>Optional human title.</summary>
    public string? Title { get; init; }
}
