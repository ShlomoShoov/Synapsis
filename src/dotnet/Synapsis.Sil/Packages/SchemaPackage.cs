using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Synapsis.Sil.Packages;

/// <summary>
/// A schema package: the vocabulary one source (or the core) adds to the record language. It declares sources,
/// record types, roles, measures and logical spaces, and the id namespaces it owns. Its format is
/// schemas/sil/schema-package.schema.json; the files are feeders/packages/*.yaml and schemas/seed/core.yaml.
/// </summary>
public sealed class SchemaPackage
{
    /// <summary>The package's name, for example "kev" (the "package" key in YAML).</summary>
    [YamlMember(Alias = "package")]
    public string Name { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional longer description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional: who looks after this package.</summary>
    public string? Maintainer { get; init; }

    /// <summary>The id namespaces this package owns.</summary>
    public PackageNamespaces Namespaces { get; init; } = new PackageNamespaces();

    /// <summary>Core only: the closed lists the record rules use (precision, known_at_basis, country, ...), by name.</summary>
    public Dictionary<string, List<string>> Vocabularies { get; init; } = new Dictionary<string, List<string>>();

    /// <summary>Core only: the authority namespaces any package may create entities in (cve, country, ...).</summary>
    public List<SharedNamespace> SharedEntityNamespaces { get; init; } = new List<SharedNamespace>();

    /// <summary>The sources this package declares.</summary>
    public List<SourceDefinition> Sources { get; init; } = new List<SourceDefinition>();

    /// <summary>The entity types this package declares.</summary>
    public List<EntityTypeDefinition> EntityTypes { get; init; } = new List<EntityTypeDefinition>();

    /// <summary>The event types this package declares.</summary>
    public List<EventTypeDefinition> EventTypes { get; init; } = new List<EventTypeDefinition>();

    /// <summary>The relation types this package declares.</summary>
    public List<RelationTypeDefinition> RelationTypes { get; init; } = new List<RelationTypeDefinition>();

    /// <summary>The conclusion types this package declares.</summary>
    public List<ConclusionTypeDefinition> ConclusionTypes { get; init; } = new List<ConclusionTypeDefinition>();

    /// <summary>The roles this package declares.</summary>
    public List<RoleDefinition> Roles { get; init; } = new List<RoleDefinition>();

    /// <summary>The measures this package declares.</summary>
    public List<MeasureDefinition> Measures { get; init; } = new List<MeasureDefinition>();

    /// <summary>The logical spaces this package declares.</summary>
    public List<LogicalSpaceDefinition> LogicalSpaces { get; init; } = new List<LogicalSpaceDefinition>();
}
