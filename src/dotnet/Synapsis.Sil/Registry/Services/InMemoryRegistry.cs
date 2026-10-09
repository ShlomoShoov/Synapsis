using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Registry.Services;

/// <summary>
/// The registry kept in memory as lookup tables (dictionaries and sets), filled once by <see cref="RegistryBuilder"/> and read-only
/// afterwards. Each question of <see cref="IRegistry"/> is one lookup.
/// </summary>
public sealed class InMemoryRegistry : IRegistry
{
    /// <summary>The closed lists of core, by name: precision, known_at_basis, reliability_basis, method_by, country.</summary>
    public required IReadOnlyDictionary<string, HashSet<string>> Vocabularies { get; init; }

    /// <summary>Every source, by id.</summary>
    public required IReadOnlyDictionary<string, SourceDefinition> Sources { get; init; }

    /// <summary>The declaring package of every source, by source id.</summary>
    public required IReadOnlyDictionary<string, string> SourcePackages { get; init; }

    /// <summary>The owning package of every records namespace.</summary>
    public required IReadOnlyDictionary<string, string> RecordNamespaces { get; init; }

    /// <summary>The owning package of every entity namespace.</summary>
    public required IReadOnlyDictionary<string, string> EntityNamespaces { get; init; }

    /// <summary>Every shared entity namespace, by name.</summary>
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

    /// <summary>Every pattern the vocabulary uses, compiled once, by its text.</summary>
    public required IReadOnlyDictionary<string, Regex> Patterns { get; init; }

    /// <inheritdoc/>
    public bool IsPrecision(string precision)
    {
        return InVocabulary("precision", precision);
    }

    /// <inheritdoc/>
    public bool IsKnownAtBasis(string basis)
    {
        return InVocabulary("known_at_basis", basis);
    }

    /// <inheritdoc/>
    public bool IsReliabilityBasis(string basis)
    {
        return InVocabulary("reliability_basis", basis);
    }

    /// <inheritdoc/>
    public bool IsMethodBy(string methodBy)
    {
        return InVocabulary("method_by", methodBy);
    }

    /// <inheritdoc/>
    public bool IsCountry(string countryCode)
    {
        return InVocabulary("country", countryCode);
    }

    /// <inheritdoc/>
    public SourceDefinition? FindSource(string sourceId)
    {
        return Sources.GetValueOrDefault(sourceId);
    }

    /// <inheritdoc/>
    public string? PackageOfSource(string sourceId)
    {
        return SourcePackages.GetValueOrDefault(sourceId);
    }

    /// <inheritdoc/>
    public string? OwnerOfRecordNamespace(string space)
    {
        return RecordNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public string? OwnerOfEntityNamespace(string space)
    {
        return EntityNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public SharedNamespace? FindSharedNamespace(string space)
    {
        return SharedNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public EventTypeDefinition? FindEventType(string code)
    {
        return EventTypes.GetValueOrDefault(code);
    }

    /// <inheritdoc/>
    public RelationTypeDefinition? FindRelationType(string code)
    {
        return RelationTypes.GetValueOrDefault(code);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public MeasureDefinition? FindMeasure(string name)
    {
        return Measures.GetValueOrDefault(name);
    }

    /// <inheritdoc/>
    public LogicalSpaceDefinition? FindLogicalSpace(string space)
    {
        return LogicalSpaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public bool Matches(string pattern, string text)
    {
        return Patterns[pattern].IsMatch(text);
    }

    /// <summary>True when the value is in the named closed list of core.</summary>
    private bool InVocabulary(string name, string value)
    {
        return Vocabularies.TryGetValue(name, out HashSet<string>? values) && values.Contains(value);
    }
}
