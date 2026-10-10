using System.Collections.Generic;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Package;

/// <summary>
/// Every code the package points to must exist in some package (this one or another): the roles, entity types and
/// measures its event types use, the entity types at its relations' ends and its shared namespaces, its sources'
/// reliability basis. A closed logical space needs values, and an entity namespace may not be a shared one.
/// </summary>
public sealed class PackageReferenceRule : IPackageRule
{
    /// <summary>The core vocabulary every source's reliability basis must come from.</summary>
    private const string ReliabilityBasisVocabulary = "reliability_basis";

    /// <summary>Passes when every reference resolves; E-SCHEMA-REF with the first one that does not otherwise.</summary>
    public Result Check(PackageValidationRequest request)
    {
        List<SchemaPackage> all = new List<SchemaPackage>(request.Others);
        all.Add(request.Package);
        List<string> problems = new List<string>();
        AddTypeReferences(request.Package, all, problems);
        AddOtherReferences(request.Package, all, problems);
        if (problems.Count == 0)
        {
            return Result.Success();
        }
        return Result.Failure(ErrorCodes.SchemaRef, string.Empty, problems[0]);
    }

    /// <summary>Roles, entity types and measures named by the package's event and relation types.</summary>
    private static void AddTypeReferences(SchemaPackage package, List<SchemaPackage> all, List<string> problems)
    {
        HashSet<string> entityTypes = EntityTypes(all);
        HashSet<string> roles = Roles(all);
        HashSet<string> measures = Measures(all);
        foreach (EventTypeDefinition type in package.EventTypes)
        {
            foreach (KeyValuePair<string, List<string>> role in type.Roles)
            {
                AddUnknown("role", role.Key, roles, $"event type '{type.Code}'", problems);
                AddUnknown("entity type", role.Value, entityTypes, $"role '{role.Key}' of event type '{type.Code}'", problems);
            }
            AddUnknown("measure", type.Measures, measures, $"event type '{type.Code}'", problems);
        }
        foreach (RelationTypeDefinition type in package.RelationTypes)
        {
            AddUnknown("entity type", type.From, entityTypes, $"relation type '{type.Code}'", problems);
            AddUnknown("entity type", type.To, entityTypes, $"relation type '{type.Code}'", problems);
        }
    }

    /// <summary>Shared namespaces, sources, logical spaces and entity namespaces.</summary>
    private static void AddOtherReferences(SchemaPackage package, List<SchemaPackage> all, List<string> problems)
    {
        HashSet<string> entityTypes = EntityTypes(all);
        foreach (SharedNamespace shared in package.SharedEntityNamespaces)
        {
            AddUnknown("entity type", shared.EntityType, entityTypes, $"shared namespace '{shared.Namespace}'", problems);
        }
        HashSet<string>? bases = Vocabulary(all, ReliabilityBasisVocabulary);
        foreach (SourceDefinition source in package.Sources)
        {
            if (bases != null)
            {
                AddUnknown("reliability basis", source.ReliabilityBasis, bases, $"source '{source.Id}'", problems);
            }
        }
        foreach (LogicalSpaceDefinition space in package.LogicalSpaces)
        {
            if (space.Closed && space.Values.Count == 0)
            {
                problems.Add($"the closed logical space '{space.Space}' has no values");
            }
        }
        HashSet<string> shareds = SharedNamespaces(all);
        foreach (string entityNamespace in package.Namespaces.Entities)
        {
            if (shareds.Contains(entityNamespace))
            {
                problems.Add($"the entity namespace '{entityNamespace}' is a shared namespace, owned by core");
            }
        }
    }

    /// <summary>Adds "unknown {what} '{code}' in {where}" when the code is not among the known ones.</summary>
    private static void AddUnknown(string what, string code, HashSet<string> known, string where, List<string> problems)
    {
        if (!known.Contains(code))
        {
            problems.Add($"unknown {what} '{code}' in {where}");
        }
    }

    /// <summary>Adds a problem for every code of the list that is not among the known ones.</summary>
    private static void AddUnknown(string what, List<string> codes, HashSet<string> known, string where, List<string> problems)
    {
        foreach (string code in codes)
        {
            AddUnknown(what, code, known, where, problems);
        }
    }

    /// <summary>Every entity type code of the packages.</summary>
    private static HashSet<string> EntityTypes(List<SchemaPackage> packages)
    {
        HashSet<string> codes = new HashSet<string>();
        foreach (SchemaPackage package in packages)
        {
            foreach (EntityTypeDefinition type in package.EntityTypes)
            {
                codes.Add(type.Code);
            }
        }
        return codes;
    }

    /// <summary>Every role code of the packages.</summary>
    private static HashSet<string> Roles(List<SchemaPackage> packages)
    {
        HashSet<string> codes = new HashSet<string>();
        foreach (SchemaPackage package in packages)
        {
            foreach (RoleDefinition role in package.Roles)
            {
                codes.Add(role.Code);
            }
        }
        return codes;
    }

    /// <summary>Every measure name of the packages.</summary>
    private static HashSet<string> Measures(List<SchemaPackage> packages)
    {
        HashSet<string> names = new HashSet<string>();
        foreach (SchemaPackage package in packages)
        {
            foreach (MeasureDefinition measure in package.Measures)
            {
                names.Add(measure.Name);
            }
        }
        return names;
    }

    /// <summary>Every shared namespace of the packages.</summary>
    private static HashSet<string> SharedNamespaces(List<SchemaPackage> packages)
    {
        HashSet<string> names = new HashSet<string>();
        foreach (SchemaPackage package in packages)
        {
            foreach (SharedNamespace shared in package.SharedEntityNamespaces)
            {
                names.Add(shared.Namespace);
            }
        }
        return names;
    }

    /// <summary>The values of the named vocabulary, or null when no package declares it.</summary>
    private static HashSet<string>? Vocabulary(List<SchemaPackage> packages, string name)
    {
        foreach (SchemaPackage package in packages)
        {
            if (package.Vocabularies.TryGetValue(name, out List<string>? values))
            {
                return new HashSet<string>(values);
            }
        }
        return null;
    }
}
