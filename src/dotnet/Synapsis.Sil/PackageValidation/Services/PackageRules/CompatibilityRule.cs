using System;
using System.Collections.Generic;
using System.Linq;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Domain.Vocabulary.Terms;
using Synapsis.Sil.Models.Domain.Vocabulary.Types;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Abstractions;
using Synapsis.Sil.PackageValidation.Models.Requests;

namespace Synapsis.Sil.PackageValidation.Services.PackageRules;

/// <summary>
/// A new version of a package may add, never remove: every code, value, place form, role, measure and attribute its
/// stored version declared must still be there, and no measure may change its unit (stored values would silently mean
/// something else). Stored records depend on all of these. Other narrowings (a lower maximum, a new required
/// attribute) are allowed for now; they only tighten future records.
/// </summary>
public sealed class CompatibilityRule : IPackageRule
{
    /// <summary>Passes for a new package or a compatible version; E-SCHEMA-BREAKING with the first problem otherwise.</summary>
    public Result Check(PackageValidationRequest request)
    {
        if (request.Stored == null)
        {
            return Result.Success();
        }
        List<string> problems = new List<string>();
        AddRemovedDefinitions(request.Stored, request.Package, problems);
        AddNarrowedEventTypes(request.Stored, request.Package, problems);
        AddNarrowedOtherTypes(request.Stored, request.Package, problems);
        AddNarrowedTerms(request.Stored, request.Package, problems);
        if (problems.Count == 0)
        {
            return Result.Success();
        }
        return Result.Failure(ErrorCodes.SchemaBreaking, string.Empty, problems[0] + " (stored records depend on it: add a new code instead)");
    }

    /// <summary>Whole definitions: sources, types, roles, measures, spaces, namespaces, vocabularies.</summary>
    private static void AddRemovedDefinitions(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        AddRemoved("source", "", before.Sources.Select(s => s.Id), after.Sources.Select(s => s.Id), problems);
        AddRemoved("entity type", "", before.EntityTypes.Select(t => t.Code), after.EntityTypes.Select(t => t.Code), problems);
        AddRemoved("event type", "", before.EventTypes.Select(t => t.Code), after.EventTypes.Select(t => t.Code), problems);
        AddRemoved("relation type", "", before.RelationTypes.Select(t => t.Code), after.RelationTypes.Select(t => t.Code), problems);
        AddRemoved("conclusion type", "", before.ConclusionTypes.Select(t => t.Code), after.ConclusionTypes.Select(t => t.Code), problems);
        AddRemoved("role", "", before.Roles.Select(r => r.Code), after.Roles.Select(r => r.Code), problems);
        AddRemoved("measure", "", before.Measures.Select(m => m.Name), after.Measures.Select(m => m.Name), problems);
        AddRemoved("logical space", "", before.LogicalSpaces.Select(s => s.Space), after.LogicalSpaces.Select(s => s.Space), problems);
        AddRemoved("records namespace", "", before.Namespaces.Records, after.Namespaces.Records, problems);
        AddRemoved("entity namespace", "", before.Namespaces.Entities, after.Namespaces.Entities, problems);
        AddRemoved("shared namespace", "", before.SharedEntityNamespaces.Select(n => n.Namespace), after.SharedEntityNamespaces.Select(n => n.Namespace), problems);
        AddRemoved("vocabulary", "", before.Vocabularies.Keys, after.Vocabularies.Keys, problems);
    }

    /// <summary>Inside each event type kept: place forms, roles and their entity types, measures, attributes.</summary>
    private static void AddNarrowedEventTypes(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        Dictionary<string, EventTypeDefinition> kept = Index(after.EventTypes, t => t.Code);
        foreach (EventTypeDefinition old in before.EventTypes)
        {
            if (!kept.TryGetValue(old.Code, out EventTypeDefinition? now))
            {
                continue;
            }
            string where = $" of event type '{old.Code}'";
            AddRemoved("place form", where, old.Place.Select(p => p.ToString()), now.Place.Select(p => p.ToString()), problems);
            AddRemoved("role", where, old.Roles.Keys, now.Roles.Keys, problems);
            foreach (KeyValuePair<string, List<string>> role in old.Roles)
            {
                if (now.Roles.TryGetValue(role.Key, out List<string>? allowed))
                {
                    AddRemoved("entity type", $" in role '{role.Key}'{where}", role.Value, allowed, problems);
                }
            }
            AddRemoved("measure", where, old.Measures, now.Measures, problems);
            AddRemoved("attribute", where, old.Attributes.Keys, now.Attributes.Keys, problems);
        }
    }

    /// <summary>Inside each entity and relation type kept: attributes, and a relation's allowed ends.</summary>
    private static void AddNarrowedOtherTypes(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        Dictionary<string, EntityTypeDefinition> entities = Index(after.EntityTypes, t => t.Code);
        foreach (EntityTypeDefinition old in before.EntityTypes)
        {
            if (entities.TryGetValue(old.Code, out EntityTypeDefinition? now))
            {
                AddRemoved("attribute", $" of entity type '{old.Code}'", old.Attributes.Keys, now.Attributes.Keys, problems);
            }
        }
        Dictionary<string, RelationTypeDefinition> relations = Index(after.RelationTypes, t => t.Code);
        foreach (RelationTypeDefinition old in before.RelationTypes)
        {
            if (relations.TryGetValue(old.Code, out RelationTypeDefinition? now))
            {
                string where = $" of relation type '{old.Code}'";
                AddRemoved("'from' entity type", where, old.From, now.From, problems);
                AddRemoved("'to' entity type", where, old.To, now.To, problems);
                AddRemoved("attribute", where, old.Attributes.Keys, now.Attributes.Keys, problems);
            }
        }
    }

    /// <summary>Vocabulary values and logical-space values kept; measure units unchanged.</summary>
    private static void AddNarrowedTerms(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        foreach (KeyValuePair<string, List<string>> vocabulary in before.Vocabularies)
        {
            if (after.Vocabularies.TryGetValue(vocabulary.Key, out List<string>? now))
            {
                AddRemoved("value", $" of vocabulary '{vocabulary.Key}'", vocabulary.Value, now, problems);
            }
        }
        Dictionary<string, LogicalSpaceDefinition> spaces = Index(after.LogicalSpaces, s => s.Space);
        foreach (LogicalSpaceDefinition old in before.LogicalSpaces)
        {
            if (spaces.TryGetValue(old.Space, out LogicalSpaceDefinition? now))
            {
                AddRemoved("value", $" of logical space '{old.Space}'", old.Values, now.Values, problems);
            }
        }
        Dictionary<string, MeasureDefinition> measures = Index(after.Measures, m => m.Name);
        foreach (MeasureDefinition old in before.Measures)
        {
            if (measures.TryGetValue(old.Name, out MeasureDefinition? now) && now.Unit != old.Unit)
            {
                problems.Add($"the unit of measure '{old.Name}' changed from '{old.Unit}' to '{now.Unit}'");
            }
        }
    }

    /// <summary>Adds "{what} '{item}'{where} was removed" for every item of <paramref name="before"/> missing from <paramref name="after"/>.</summary>
    private static void AddRemoved(string what, string where, IEnumerable<string> before, IEnumerable<string> after, List<string> problems)
    {
        HashSet<string> kept = new HashSet<string>(after);
        foreach (string item in before)
        {
            if (!kept.Contains(item))
            {
                problems.Add($"{what} '{item}'{where} was removed");
            }
        }
    }

    /// <summary>The definitions by their code.</summary>
    private static Dictionary<string, T> Index<T>(IEnumerable<T> definitions, Func<T, string> codeOf)
    {
        Dictionary<string, T> index = new Dictionary<string, T>();
        foreach (T definition in definitions)
        {
            index[codeOf(definition)] = definition;
        }
        return index;
    }
}
