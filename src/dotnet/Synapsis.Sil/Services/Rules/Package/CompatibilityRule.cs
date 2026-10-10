using System.Collections.Generic;
using System.Linq;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Package;

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
        DefinitionDiff.AddRemoved("source", "", before.Sources.Select(s => s.Id), after.Sources.Select(s => s.Id), problems);
        DefinitionDiff.AddRemoved("entity type", "", before.EntityTypes.Select(t => t.Code), after.EntityTypes.Select(t => t.Code), problems);
        DefinitionDiff.AddRemoved("event type", "", before.EventTypes.Select(t => t.Code), after.EventTypes.Select(t => t.Code), problems);
        DefinitionDiff.AddRemoved("relation type", "", before.RelationTypes.Select(t => t.Code), after.RelationTypes.Select(t => t.Code), problems);
        DefinitionDiff.AddRemoved("conclusion type", "", before.ConclusionTypes.Select(t => t.Code), after.ConclusionTypes.Select(t => t.Code), problems);
        DefinitionDiff.AddRemoved("role", "", before.Roles.Select(r => r.Code), after.Roles.Select(r => r.Code), problems);
        DefinitionDiff.AddRemoved("measure", "", before.Measures.Select(m => m.Name), after.Measures.Select(m => m.Name), problems);
        DefinitionDiff.AddRemoved("logical space", "", before.LogicalSpaces.Select(s => s.Space), after.LogicalSpaces.Select(s => s.Space), problems);
        DefinitionDiff.AddRemoved("records namespace", "", before.Namespaces.Records, after.Namespaces.Records, problems);
        DefinitionDiff.AddRemoved("entity namespace", "", before.Namespaces.Entities, after.Namespaces.Entities, problems);
        DefinitionDiff.AddRemoved("shared namespace", "", before.SharedEntityNamespaces.Select(n => n.Namespace), after.SharedEntityNamespaces.Select(n => n.Namespace), problems);
        DefinitionDiff.AddRemoved("vocabulary", "", before.Vocabularies.Keys, after.Vocabularies.Keys, problems);
    }

    /// <summary>Inside each event type kept: place forms, roles and their entity types, measures, attributes.</summary>
    private static void AddNarrowedEventTypes(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        Dictionary<string, EventTypeDefinition> kept = DefinitionDiff.Index(after.EventTypes, t => t.Code);
        foreach (EventTypeDefinition old in before.EventTypes)
        {
            if (!kept.TryGetValue(old.Code, out EventTypeDefinition? now))
            {
                continue;
            }
            string where = $" of event type '{old.Code}'";
            DefinitionDiff.AddRemoved("place form", where, old.Place.Select(p => p.ToString()), now.Place.Select(p => p.ToString()), problems);
            DefinitionDiff.AddRemoved("role", where, old.Roles.Keys, now.Roles.Keys, problems);
            foreach (KeyValuePair<string, List<string>> role in old.Roles)
            {
                if (now.Roles.TryGetValue(role.Key, out List<string>? allowed))
                {
                    DefinitionDiff.AddRemoved("entity type", $" in role '{role.Key}'{where}", role.Value, allowed, problems);
                }
            }
            DefinitionDiff.AddRemoved("measure", where, old.Measures, now.Measures, problems);
            DefinitionDiff.AddRemoved("attribute", where, old.Attributes.Keys, now.Attributes.Keys, problems);
        }
    }

    /// <summary>Inside each entity and relation type kept: attributes, and a relation's allowed ends.</summary>
    private static void AddNarrowedOtherTypes(SchemaPackage before, SchemaPackage after, List<string> problems)
    {
        Dictionary<string, EntityTypeDefinition> entities = DefinitionDiff.Index(after.EntityTypes, t => t.Code);
        foreach (EntityTypeDefinition old in before.EntityTypes)
        {
            if (entities.TryGetValue(old.Code, out EntityTypeDefinition? now))
            {
                DefinitionDiff.AddRemoved("attribute", $" of entity type '{old.Code}'", old.Attributes.Keys, now.Attributes.Keys, problems);
            }
        }
        Dictionary<string, RelationTypeDefinition> relations = DefinitionDiff.Index(after.RelationTypes, t => t.Code);
        foreach (RelationTypeDefinition old in before.RelationTypes)
        {
            if (relations.TryGetValue(old.Code, out RelationTypeDefinition? now))
            {
                string where = $" of relation type '{old.Code}'";
                DefinitionDiff.AddRemoved("'from' entity type", where, old.From, now.From, problems);
                DefinitionDiff.AddRemoved("'to' entity type", where, old.To, now.To, problems);
                DefinitionDiff.AddRemoved("attribute", where, old.Attributes.Keys, now.Attributes.Keys, problems);
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
                DefinitionDiff.AddRemoved("value", $" of vocabulary '{vocabulary.Key}'", vocabulary.Value, now, problems);
            }
        }
        Dictionary<string, LogicalSpaceDefinition> spaces = DefinitionDiff.Index(after.LogicalSpaces, s => s.Space);
        foreach (LogicalSpaceDefinition old in before.LogicalSpaces)
        {
            if (spaces.TryGetValue(old.Space, out LogicalSpaceDefinition? now))
            {
                DefinitionDiff.AddRemoved("value", $" of logical space '{old.Space}'", old.Values, now.Values, problems);
            }
        }
        Dictionary<string, MeasureDefinition> measures = DefinitionDiff.Index(after.Measures, m => m.Name);
        foreach (MeasureDefinition old in before.Measures)
        {
            if (measures.TryGetValue(old.Name, out MeasureDefinition? now) && now.Unit != old.Unit)
            {
                problems.Add($"the unit of measure '{old.Name}' changed from '{old.Unit}' to '{now.Unit}'");
            }
        }
    }

}
