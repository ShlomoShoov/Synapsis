using System.Collections.Generic;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;

namespace Synapsis.Sil.Services.Rules.Package;

/// <summary>
/// Every code of one kind declared across a set of packages (entity types, roles, measures, shared namespaces, a
/// closed vocabulary): what a package's references may point to.
/// </summary>
public static class PackageCodes
{
    /// <summary>Every entity type code of the packages.</summary>
    public static HashSet<string> EntityTypes(List<SchemaPackage> packages)
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
    public static HashSet<string> Roles(List<SchemaPackage> packages)
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
    public static HashSet<string> Measures(List<SchemaPackage> packages)
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
    public static HashSet<string> SharedNamespaces(List<SchemaPackage> packages)
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
    public static HashSet<string>? Vocabulary(List<SchemaPackage> packages, string name)
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
