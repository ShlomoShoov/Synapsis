using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
namespace Synapsis.Sil.Services.Registry;

/// <summary>
/// Builds an <see cref="InMemoryRegistry"/>: puts every source, namespace, type, measure and logical space of the packages
/// into lookup tables and compiles every pattern once. A code defined by two packages is refused, roles included (the
/// registry itself needs no list of roles: records are checked against the roles their event type allows).
/// </summary>
public sealed class RegistryBuilder : IRegistryBuilder
{
    /// <inheritdoc/>
    public Result<IRegistry> Build(IEnumerable<SchemaPackage> packages)
    {
        RegistryTables tables = new RegistryTables();
        foreach (SchemaPackage package in packages)
        {
            AddPackage(tables, package);
            if (tables.FirstDuplicate != null)
            {
                return Result<IRegistry>.Failure(ErrorCodes.SchemaOwnership, string.Empty, tables.FirstDuplicate);
            }
        }
        return Result<IRegistry>.Success(new InMemoryRegistry(tables));
    }

    /// <summary>Adds everything one package declares.</summary>
    private static void AddPackage(RegistryTables tables, SchemaPackage package)
    {
        foreach (KeyValuePair<string, List<string>> vocabulary in package.Vocabularies)
        {
            AddUnique(tables, tables.Vocabularies, vocabulary.Key, new HashSet<string>(vocabulary.Value), "vocabulary");
        }
        AddOwnership(tables, package);
        foreach (SourceDefinition source in package.Sources)
        {
            AddUnique(tables, tables.Sources, source.Id, source, "source");
            tables.SourcePackages[source.Id] = package.Name;
        }
        AddTypes(tables, package);
        foreach (RoleDefinition role in package.Roles)
        {
            AddUnique(tables, tables.Roles, role.Code, role, "role");
        }
        foreach (MeasureDefinition measure in package.Measures)
        {
            AddUnique(tables, tables.Measures, measure.Name, measure, "measure");
        }
        foreach (LogicalSpaceDefinition space in package.LogicalSpaces)
        {
            AddUnique(tables, tables.LogicalSpaces, space.Space, space, "logical space");
        }
    }

    /// <summary>The id namespaces the package owns, and the shared namespaces (core).</summary>
    private static void AddOwnership(RegistryTables tables, SchemaPackage package)
    {
        foreach (string space in package.Namespaces.Records)
        {
            AddUnique(tables, tables.RecordNamespaces, space, package.Name, "records namespace");
        }
        foreach (string space in package.Namespaces.Entities)
        {
            AddUnique(tables, tables.EntityNamespaces, space, package.Name, "entity namespace");
        }
        foreach (SharedNamespace shared in package.SharedEntityNamespaces)
        {
            AddUnique(tables, tables.SharedNamespaces, shared.Namespace, shared, "shared namespace");
            AddPattern(tables, shared.KeyPattern);
        }
    }

    /// <summary>The four kinds of record type, and their attribute patterns.</summary>
    private static void AddTypes(RegistryTables tables, SchemaPackage package)
    {
        foreach (EntityTypeDefinition type in package.EntityTypes)
        {
            AddUnique(tables, tables.EntityTypes, type.Code, type, "entity type");
            AddAttributePatterns(tables, type.Attributes);
        }
        foreach (EventTypeDefinition type in package.EventTypes)
        {
            AddUnique(tables, tables.EventTypes, type.Code, type, "event type");
            AddAttributePatterns(tables, type.Attributes);
        }
        foreach (RelationTypeDefinition type in package.RelationTypes)
        {
            AddUnique(tables, tables.RelationTypes, type.Code, type, "relation type");
            AddAttributePatterns(tables, type.Attributes);
        }
        foreach (ConclusionTypeDefinition type in package.ConclusionTypes)
        {
            AddUnique(tables, tables.ConclusionTypes, type.Code, type, "conclusion type");
        }
    }

    /// <summary>Compiles the pattern of every attribute that has one.</summary>
    private static void AddAttributePatterns(RegistryTables tables, Dictionary<string, AttributeDefinition> attributes)
    {
        foreach (AttributeDefinition attribute in attributes.Values)
        {
            if (attribute.Pattern != null)
            {
                AddPattern(tables, attribute.Pattern);
            }
        }
    }

    /// <summary>Compiles a pattern once; the same text used twice shares one compiled expression.</summary>
    private static void AddPattern(RegistryTables tables, string pattern)
    {
        if (!tables.Patterns.ContainsKey(pattern))
        {
            tables.Patterns[pattern] = new Regex(pattern, RegexOptions.Compiled);
        }
    }

    /// <summary>Adds a definition; a second definition of the same code is remembered as the problem.</summary>
    private static void AddUnique<T>(RegistryTables tables, Dictionary<string, T> table, string code, T definition, string what)
    {
        if (table.ContainsKey(code))
        {
            if (tables.FirstDuplicate == null)
            {
                tables.FirstDuplicate = $"the {what} '{code}' is defined by two packages";
            }
            return;
        }
        table[code] = definition;
    }
}
