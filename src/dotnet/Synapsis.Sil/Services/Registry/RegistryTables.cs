using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;

namespace Synapsis.Sil.Services.Registry;

/// <summary>
/// The vocabulary as lookup tables (dictionaries and sets): filled once by <see cref="RegistryBuilder"/>, then only read
/// by <see cref="InMemoryRegistry"/>.
/// </summary>
public sealed class RegistryTables
{
    /// <summary>The closed lists of core, by name: precision, known_at_basis, reliability_basis, method_by, country.</summary>
    public Dictionary<string, HashSet<string>> Vocabularies { get; } = new Dictionary<string, HashSet<string>>();

    /// <summary>Every source, by id.</summary>
    public Dictionary<string, SourceDefinition> Sources { get; } = new Dictionary<string, SourceDefinition>();

    /// <summary>The declaring package of every source, by source id.</summary>
    public Dictionary<string, string> SourcePackages { get; } = new Dictionary<string, string>();

    /// <summary>The owning package of every records namespace.</summary>
    public Dictionary<string, string> RecordNamespaces { get; } = new Dictionary<string, string>();

    /// <summary>The owning package of every entity namespace.</summary>
    public Dictionary<string, string> EntityNamespaces { get; } = new Dictionary<string, string>();

    /// <summary>Every shared entity namespace, by name.</summary>
    public Dictionary<string, SharedNamespace> SharedNamespaces { get; } = new Dictionary<string, SharedNamespace>();

    /// <summary>Every entity type, by code.</summary>
    public Dictionary<string, EntityTypeDefinition> EntityTypes { get; } = new Dictionary<string, EntityTypeDefinition>();

    /// <summary>Every event type, by code.</summary>
    public Dictionary<string, EventTypeDefinition> EventTypes { get; } = new Dictionary<string, EventTypeDefinition>();

    /// <summary>Every relation type, by code.</summary>
    public Dictionary<string, RelationTypeDefinition> RelationTypes { get; } = new Dictionary<string, RelationTypeDefinition>();

    /// <summary>Every conclusion type, by code.</summary>
    public Dictionary<string, ConclusionTypeDefinition> ConclusionTypes { get; } = new Dictionary<string, ConclusionTypeDefinition>();

    /// <summary>Every role, by code (kept only to refuse a role defined twice).</summary>
    public Dictionary<string, RoleDefinition> Roles { get; } = new Dictionary<string, RoleDefinition>();

    /// <summary>Every measure, by name.</summary>
    public Dictionary<string, MeasureDefinition> Measures { get; } = new Dictionary<string, MeasureDefinition>();

    /// <summary>Every logical space, by name.</summary>
    public Dictionary<string, LogicalSpaceDefinition> LogicalSpaces { get; } = new Dictionary<string, LogicalSpaceDefinition>();

    /// <summary>Every pattern the vocabulary uses, compiled once, by its text.</summary>
    public Dictionary<string, Regex> Patterns { get; } = new Dictionary<string, Regex>();

    /// <summary>The first code found defined by two packages, as a message; null while there is none.</summary>
    public string? FirstDuplicate { get; set; }
}
