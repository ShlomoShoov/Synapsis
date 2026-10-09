using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Records;
using Synapsis.Sil.Vocabulary.Attributes;
using Synapsis.Sil.Vocabulary.Ownership;
using Synapsis.Sil.Vocabulary.Sources;
using Synapsis.Sil.Vocabulary.Terms;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Registry;

/// <summary>
/// The vocabulary of all registered schema packages, arranged for fast lookups by the record rules: "is this source
/// declared?", "which package owns this id namespace?", "which measures does this event type allow?".
/// Built once by <see cref="RegistryBuilder"/> and never changed afterwards, so every record of a batch is checked
/// against the same vocabulary.
/// </summary>
public sealed class RegistrySnapshot
{
    /// <summary>The precisions a time may have (core): second, minute, hour, day, week, month, quarter, year.</summary>
    public required IReadOnlySet<string> Precisions { get; init; }

    /// <summary>The ways a known_at time may be established (core): published, observed, ...</summary>
    public required IReadOnlySet<string> KnownAtBases { get; init; }

    /// <summary>The reliability bases (core): the NATO codes A1 to F6, and measured, analyst, default, model.</summary>
    public required IReadOnlySet<string> ReliabilityBases { get; init; }

    /// <summary>Who can produce a conclusion (core): human, agent, system.</summary>
    public required IReadOnlySet<string> MethodBy { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 country codes (core).</summary>
    public required IReadOnlySet<string> Countries { get; init; }

    /// <summary>Every declared source, by id.</summary>
    public required IReadOnlyDictionary<string, SourceDefinition> Sources { get; init; }

    /// <summary>For each source id, the name of the package that declares it.</summary>
    public required IReadOnlyDictionary<string, string> SourcePackages { get; init; }

    /// <summary>For each namespace of event, relation and conclusion ids, the package that owns it.</summary>
    public required IReadOnlyDictionary<string, string> RecordNamespaces { get; init; }

    /// <summary>For each namespace of entity ids, the package that owns it.</summary>
    public required IReadOnlyDictionary<string, string> EntityNamespaces { get; init; }

    /// <summary>The shared entity namespaces any package may use (cve, country, ...), by namespace.</summary>
    public required IReadOnlyDictionary<string, SharedNamespace> SharedNamespaces { get; init; }

    /// <summary>Every entity type, by code.</summary>
    public required IReadOnlyDictionary<string, EntityTypeDefinition> EntityTypes { get; init; }

    /// <summary>Every event type, by code.</summary>
    public required IReadOnlyDictionary<string, EventTypeDefinition> EventTypes { get; init; }

    /// <summary>Every relation type, by code.</summary>
    public required IReadOnlyDictionary<string, RelationTypeDefinition> RelationTypes { get; init; }

    /// <summary>Every conclusion type, by code.</summary>
    public required IReadOnlyDictionary<string, ConclusionTypeDefinition> ConclusionTypes { get; init; }

    /// <summary>Every measure, by name.</summary>
    public required IReadOnlyDictionary<string, MeasureDefinition> Measures { get; init; }

    /// <summary>Every logical space, by name.</summary>
    public required IReadOnlyDictionary<string, LogicalSpaceDefinition> LogicalSpaces { get; init; }

    /// <summary>Every regular expression the vocabulary uses (attribute patterns, shared key patterns), compiled once.</summary>
    public required IReadOnlyDictionary<string, Regex> Patterns { get; init; }

    /// <summary>True when a type with this code is registered for this kind of record.</summary>
    public bool HasType(RecordKind kind, string code)
    {
        switch (kind)
        {
            case RecordKind.Entity:
                return EntityTypes.ContainsKey(code);
            case RecordKind.Event:
                return EventTypes.ContainsKey(code);
            case RecordKind.Relation:
                return RelationTypes.ContainsKey(code);
            default:
                return ConclusionTypes.ContainsKey(code);
        }
    }

    /// <summary>
    /// The attributes a type declares, by name. Conclusion types declare none, and an unknown type has none.
    /// </summary>
    public IReadOnlyDictionary<string, AttributeDefinition> AttributesOf(RecordKind kind, string code)
    {
        if (kind == RecordKind.Entity && EntityTypes.TryGetValue(code, out EntityTypeDefinition? entityType))
        {
            return entityType.Attributes;
        }
        if (kind == RecordKind.Event && EventTypes.TryGetValue(code, out EventTypeDefinition? eventType))
        {
            return eventType.Attributes;
        }
        if (kind == RecordKind.Relation && RelationTypes.TryGetValue(code, out RelationTypeDefinition? relationType))
        {
            return relationType.Attributes;
        }
        return new Dictionary<string, AttributeDefinition>();
    }
}
