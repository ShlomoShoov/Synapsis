using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Registry.Services;

/// <summary>
/// Builds an <see cref="InMemoryRegistry"/>: puts every source, namespace, type, measure and logical space of the packages
/// into lookup tables and compiles every pattern once. A code defined by two packages is refused.
/// </summary>
public sealed class RegistryBuilder : IRegistryBuilder
{
    /// <inheritdoc/>
    public Result<IRegistry> Build(IEnumerable<SchemaPackage> packages)
    {
        Tables tables = new Tables();
        foreach (SchemaPackage package in packages)
        {
            string? duplicate = tables.Add(package);
            if (duplicate != null)
            {
                return Result<IRegistry>.Failure(ErrorCodes.SchemaOwnership, string.Empty, duplicate);
            }
        }
        return Result<IRegistry>.Success(tables.ToRegistry());
    }

    /// <summary>The tables being filled, one package at a time.</summary>
    private sealed class Tables
    {
        private readonly Dictionary<string, HashSet<string>> _vocabularies = new Dictionary<string, HashSet<string>>();
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
        private string? _duplicate;

        /// <summary>Adds everything one package declares; returns a message when a code was already defined, else null.</summary>
        public string? Add(SchemaPackage package)
        {
            foreach (KeyValuePair<string, List<string>> vocabulary in package.Vocabularies)
            {
                AddUnique(_vocabularies, vocabulary.Key, new HashSet<string>(vocabulary.Value), "vocabulary");
            }
            AddOwnership(package);
            foreach (SourceDefinition source in package.Sources)
            {
                AddUnique(_sources, source.Id, source, "source");
                _sourcePackages[source.Id] = package.Name;
            }
            AddTypes(package);
            foreach (MeasureDefinition measure in package.Measures)
            {
                AddUnique(_measures, measure.Name, measure, "measure");
            }
            foreach (LogicalSpaceDefinition space in package.LogicalSpaces)
            {
                AddUnique(_logicalSpaces, space.Space, space, "logical space");
            }
            return _duplicate;
        }

        /// <summary>Freezes the tables into a registry.</summary>
        public InMemoryRegistry ToRegistry()
        {
            return new InMemoryRegistry
            {
                Vocabularies = _vocabularies,
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

        /// <summary>The id namespaces the package owns, and the shared namespaces (core).</summary>
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

        /// <summary>The four kinds of record type, and their attribute patterns.</summary>
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

        /// <summary>Adds a definition; a second definition of the same code is remembered as the problem.</summary>
        private void AddUnique<T>(Dictionary<string, T> table, string code, T definition, string what)
        {
            if (table.ContainsKey(code))
            {
                if (_duplicate == null)
                {
                    _duplicate = $"the {what} '{code}' is defined by two packages";
                }
                return;
            }
            table[code] = definition;
        }
    }
}
