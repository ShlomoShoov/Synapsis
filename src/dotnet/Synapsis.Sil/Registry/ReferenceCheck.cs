using System.Text.Json.Nodes;
using static Synapsis.Sil.Registry.Definitions;

namespace Synapsis.Sil.Registry;

/// <summary>
/// Every code a definition uses must exist in the registry as it would be after the change
/// (<c>422 E-SCHEMA-REF</c>). A package may use codes other packages own, such as
/// <c>Vulnerability</c> and <c>subject</c> from <c>core</c>.
/// </summary>
public static class ReferenceCheck
{
    /// <summary>One line per missing reference; empty when every reference resolves.</summary>
    public static List<string> Check(IReadOnlyDictionary<string, JsonObject> all)
    {
        var problems = new List<string>();

        foreach (var key in all.Keys.Order(StringComparer.Ordinal))
        {
            var definition = all[key];
            void Require(string kind, string? code)
            {
                if (code is not null && !all.ContainsKey(Key(kind, code)))
                    problems.Add($"{key} uses {kind} '{code}', which no package defines");
            }

            switch (Split(key).Kind)
            {
                case EventType:
                    foreach (var (role, entityTypes) in definition["roles"] as JsonObject ?? [])
                    {
                        Require(Role, role);
                        foreach (var entityType in Strings(entityTypes))
                            Require(EntityType, entityType);
                    }
                    foreach (var measure in Strings(definition["measures"]))
                        Require(Measure, measure);
                    break;
                case RelationType:
                    foreach (var entityType in Strings(definition["from"]).Concat(Strings(definition["to"])))
                        Require(EntityType, entityType);
                    break;
                case ConclusionType:
                    Require(LogicalSpace, Text(definition["space"]));
                    break;
                case SharedNamespace:
                    Require(EntityType, Text(definition["entity_type"]));
                    break;
            }
        }

        return problems;
    }
}
