using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Vocabulary;
using Synapsis.Sil.Vocabulary.Attributes;
using Synapsis.Sil.Vocabulary.Ownership;
using Synapsis.Sil.Vocabulary.Sources;
using Synapsis.Sil.Vocabulary.Terms;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Registry;

/// <summary>
/// Builds a <see cref="RegistrySnapshot"/> from schema packages: puts every source, namespace, type, measure and
/// logical space into lookup tables, and compiles every pattern once. Each code may be defined by one package only;
/// a code defined twice is a broken registry and stops the build.
/// </summary>
public sealed class RegistryBuilder
{
    private readonly Dictionary<string, List<string>> _vocabularies = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, SourceDefinition> _sources = new Dictionary<string, SourceDefinition>();
    private readonly Dictionary<string, string> _sourcePackages = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _recordNamespaces = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _entityNamespaces = new Dictionary<string, string>();
    private readonly Dictionary<string, SharedNamespace> _sharedNamespaces = new Dictionary<string, SharedNamespace>();
    private readonly Dictionary<string, EntityTypeDefinition> _entityTypes = new Dictionary<string, EntityTypeDefinition>();
    private readonly Dictionary<string, EventTypeDefinition> _eventTypes = new Dictionary<string, EventTypeDefinition>();
    private readonly Dictionary<string, RelationTypeDefinition> _relationTypes = new Dictionary<string, RelationTypeDefinition>();
    private readonly Dictionary<string, ConclusionTypeDefinition> _conclusionTypes = new Dictionary<string, ConclusionTypeDefinition>();
    private readonly Dictionary<string, MeasureDefinition> _measures = new Dictionary<string, MeasureDefinition>();
    private readonly Dictionary<string, LogicalSpaceDefinition> _logicalSpaces = new Dictionary<string, LogicalSpaceDefinition>();
    private readonly Dictionary<string, Regex> _patterns = new Dictionary<string, Regex>();

    /// <summary>Builds the snapshot of all the given packages (core and every source package).</summary>
    public static RegistrySnapshot Build(IEnumerable<SchemaPackage> packages)
    {
        RegistryBuilder builder = new RegistryBuilder();
        foreach (SchemaPackage package in packages)
        {
            builder.Add(package);
        }
        return builder.ToSnapshot();
    }

    /// <summary>Adds everything one package declares.</summary>
    private void Add(SchemaPackage package)
    {
        foreach (KeyValuePair<string, List<string>> vocabulary in package.Vocabularies)
        {
            AddUnique(_vocabularies, vocabulary.Key, vocabulary.Value, "vocabulary");
        }
        AddOwnership(package);
        foreach (SourceDefinition source in package.Sources)
        {
            AddUnique(_sources, source.Id, source, "source");
            _sourcePackages[source.Id] = package.Name;
        }
        AddTypes(package);
        AddTerms(package);
    }

    /// <summary>Adds the id namespaces the package owns, and the shared namespaces (core).</summary>
    private void AddOwnership(SchemaPackage package)
    {
        foreach (string space in package.Namespaces.Records)
        {
            AddUnique(_recordNamespaces, space, package.Name, "records namespace");
        }
        foreach (string space in package.Namespaces.Entities)
        {
            AddUnique(_entityNamespaces, space, package.Name, "entity namespace");
        }
        foreach (SharedNamespace shared in package.SharedEntityNamespaces)
        {
            AddUnique(_sharedNamespaces, shared.Namespace, shared, "shared namespace");
            AddPattern(shared.KeyPattern);
        }
    }

    /// <summary>Adds the four kinds of record type, and compiles their attribute patterns.</summary>
    private void AddTypes(SchemaPackage package)
    {
        foreach (EntityTypeDefinition type in package.EntityTypes)
        {
            AddUnique(_entityTypes, type.Code, type, "entity type");
            AddAttributePatterns(type.Attributes);
        }
        foreach (EventTypeDefinition type in package.EventTypes)
        {
            AddUnique(_eventTypes, type.Code, type, "event type");
            AddAttributePatterns(type.Attributes);
        }
        foreach (RelationTypeDefinition type in package.RelationTypes)
        {
            AddUnique(_relationTypes, type.Code, type, "relation type");
            AddAttributePatterns(type.Attributes);
        }
        foreach (ConclusionTypeDefinition type in package.ConclusionTypes)
        {
            AddUnique(_conclusionTypes, type.Code, type, "conclusion type");
        }
    }

    /// <summary>Adds the measures and logical spaces (roles are listed on each event type, so they need no table).</summary>
    private void AddTerms(SchemaPackage package)
    {
        foreach (MeasureDefinition measure in package.Measures)
        {
            AddUnique(_measures, measure.Name, measure, "measure");
        }
        foreach (LogicalSpaceDefinition space in package.LogicalSpaces)
        {
            AddUnique(_logicalSpaces, space.Space, space, "logical space");
        }
    }

    /// <summary>Compiles the pattern of every attribute that has one.</summary>
    private void AddAttributePatterns(Dictionary<string, AttributeDefinition> attributes)
    {
        foreach (AttributeDefinition attribute in attributes.Values)
        {
            if (attribute.Pattern != null)
            {
                AddPattern(attribute.Pattern);
            }
        }
    }

    /// <summary>Compiles a pattern once; the same text used twice shares one compiled expression.</summary>
    private void AddPattern(string pattern)
    {
        if (!_patterns.ContainsKey(pattern))
        {
            _patterns[pattern] = new Regex(pattern, RegexOptions.Compiled);
        }
    }

    /// <summary>Freezes the tables into a snapshot.</summary>
    private RegistrySnapshot ToSnapshot()
    {
        return new RegistrySnapshot
        {
            Precisions = VocabularySet("precision"),
            KnownAtBases = VocabularySet("known_at_basis"),
            ReliabilityBases = VocabularySet("reliability_basis"),
            MethodBy = VocabularySet("method_by"),
            Countries = VocabularySet("country"),
            Sources = _sources,
            SourcePackages = _sourcePackages,
            RecordNamespaces = _recordNamespaces,
            EntityNamespaces = _entityNamespaces,
            SharedNamespaces = _sharedNamespaces,
            EntityTypes = _entityTypes,
            EventTypes = _eventTypes,
            RelationTypes = _relationTypes,
            ConclusionTypes = _conclusionTypes,
            Measures = _measures,
            LogicalSpaces = _logicalSpaces,
            Patterns = _patterns,
        };
    }

    /// <summary>The values of one closed list from core, as a set; empty when no package declares it.</summary>
    private HashSet<string> VocabularySet(string name)
    {
        if (_vocabularies.TryGetValue(name, out List<string>? values))
        {
            return new HashSet<string>(values);
        }
        return new HashSet<string>();
    }

    /// <summary>Adds a definition, refusing a second definition of the same code.</summary>
    private static void AddUnique<T>(Dictionary<string, T> table, string code, T definition, string what)
    {
        if (table.ContainsKey(code))
        {
            throw new InvalidOperationException($"The {what} '{code}' is defined by two packages.");
        }
        table[code] = definition;
    }
}
