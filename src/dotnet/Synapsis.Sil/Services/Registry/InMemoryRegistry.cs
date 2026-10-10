using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
namespace Synapsis.Sil.Services.Registry;

/// <summary>
/// The registry kept in memory as lookup tables (dictionaries and sets), filled once by <see cref="RegistryBuilder"/> and read-only
/// afterwards (<see cref="RegistryTables"/>). Each question of <see cref="IRegistry"/> is one lookup.
/// </summary>
public sealed class InMemoryRegistry : IRegistry
{
    private readonly RegistryTables _tables;

    /// <summary>Creates the registry over tables the builder filled.</summary>
    public InMemoryRegistry(RegistryTables tables)
    {
        _tables = tables;
    }

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
        return _tables.Sources.GetValueOrDefault(sourceId);
    }

    /// <inheritdoc/>
    public string? PackageOfSource(string sourceId)
    {
        return _tables.SourcePackages.GetValueOrDefault(sourceId);
    }

    /// <inheritdoc/>
    public string? OwnerOfRecordNamespace(string space)
    {
        return _tables.RecordNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public string? OwnerOfEntityNamespace(string space)
    {
        return _tables.EntityNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public SharedNamespace? FindSharedNamespace(string space)
    {
        return _tables.SharedNamespaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public bool HasType(RecordKind kind, string code)
    {
        switch (kind)
        {
            case RecordKind.Entity:
                return _tables.EntityTypes.ContainsKey(code);
            case RecordKind.Event:
                return _tables.EventTypes.ContainsKey(code);
            case RecordKind.Relation:
                return _tables.RelationTypes.ContainsKey(code);
            default:
                return _tables.ConclusionTypes.ContainsKey(code);
        }
    }

    /// <inheritdoc/>
    public EventTypeDefinition? FindEventType(string code)
    {
        return _tables.EventTypes.GetValueOrDefault(code);
    }

    /// <inheritdoc/>
    public RelationTypeDefinition? FindRelationType(string code)
    {
        return _tables.RelationTypes.GetValueOrDefault(code);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, AttributeDefinition> AttributesOf(RecordKind kind, string code)
    {
        if (kind == RecordKind.Entity && _tables.EntityTypes.TryGetValue(code, out EntityTypeDefinition? entityType))
        {
            return entityType.Attributes;
        }
        if (kind == RecordKind.Event && _tables.EventTypes.TryGetValue(code, out EventTypeDefinition? eventType))
        {
            return eventType.Attributes;
        }
        if (kind == RecordKind.Relation && _tables.RelationTypes.TryGetValue(code, out RelationTypeDefinition? relationType))
        {
            return relationType.Attributes;
        }
        return new Dictionary<string, AttributeDefinition>();
    }

    /// <inheritdoc/>
    public MeasureDefinition? FindMeasure(string name)
    {
        return _tables.Measures.GetValueOrDefault(name);
    }

    /// <inheritdoc/>
    public LogicalSpaceDefinition? FindLogicalSpace(string space)
    {
        return _tables.LogicalSpaces.GetValueOrDefault(space);
    }

    /// <inheritdoc/>
    public bool Matches(string pattern, string text)
    {
        return _tables.Patterns[pattern].IsMatch(text);
    }

    /// <summary>True when the value is in the named closed list of core.</summary>
    private bool InVocabulary(string name, string value)
    {
        return _tables.Vocabularies.TryGetValue(name, out HashSet<string>? values) && values.Contains(value);
    }
}
