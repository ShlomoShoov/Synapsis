using System.Text.Json.Nodes;
using static Synapsis.Sil.Registry.Definitions;

namespace Synapsis.Sil.Registry;

/// <summary>
/// Compares two versions of a package's definitions. Adding and widening are allowed;
/// removing or narrowing anything stored records may use is a breaking change
/// (<c>409 E-SCHEMA-BREAKING</c>), and the fix is a new code plus a deprecation.
/// </summary>
public static class ChangeRules
{
    /// <summary>Fields that describe a definition but never make a stored record invalid.</summary>
    private static readonly HashSet<string> Descriptive =
    [
        "title", "description", "publisher", "licence", "license", "url", "known_at_rule",
        "reliability", "reliability_basis", "publication_lag_days", "deprecated",
        "code", "id", "space", "namespace",
    ];

    /// <summary>Fields <see cref="BreakingReasons"/> checks per kind; any other changed field breaks.</summary>
    private static readonly Dictionary<string, HashSet<string>> Checked = new()
    {
        [EventType] = ["place", "roles", "measures", "text", "attributes", "versioning"],
        [EntityType] = ["text", "attributes", "versioning"],
        [RelationType] = ["from", "to", "text", "attributes", "versioning"],
        [ConclusionType] = ["text", "attributes", "versioning", "space"],
        [Measure] = ["unit", "signed", "min", "max"],
        [LogicalSpace] = ["kind", "max_depth", "values"],
        [SharedNamespace] = ["pattern", "entity_type"],
        [Vocabulary] = ["values"],
    };

    /// <summary>
    /// Every change from <paramref name="old"/> to <paramref name="new"/>, and every reason
    /// the change would break stored records. An empty <c>Breaks</c> means it is compatible.
    /// </summary>
    public static (List<string> Changes, List<string> Breaks) Diff(
        IReadOnlyDictionary<string, JsonObject> old, IReadOnlyDictionary<string, JsonObject> @new)
    {
        var changes = new List<string>();
        var breaks = new List<string>();

        foreach (var key in old.Keys.Union(@new.Keys).Order(StringComparer.Ordinal))
        {
            var had = old.TryGetValue(key, out var o);
            var has = @new.TryGetValue(key, out var n);

            if (!has)
            {
                changes.Add($"removed {key}");
                breaks.Add($"{key}: removed; deprecate it instead");
                continue;
            }
            if (!had)
            {
                changes.Add($"added {key}");
                continue;
            }
            if (JsonNode.DeepEquals(o, n))
                continue;

            var wasDeprecated = IsTrue(o!["deprecated"]);
            var isDeprecated = IsTrue(n!["deprecated"]);
            if (wasDeprecated != isDeprecated)
                changes.Add(isDeprecated ? $"deprecated {key}" : $"undeprecated {key}");
            if (FieldNames(o, n).Any(f => f != "deprecated" && !JsonNode.DeepEquals(o[f], n[f])))
                changes.Add($"changed {key}");

            breaks.AddRange(BreakingReasons(key, o, n).Select(reason => $"{key}: {reason}"));
        }

        return (changes, breaks);
    }

    /// <summary>Why changing one definition from <paramref name="old"/> to <paramref name="new"/>
    /// would make records that were valid under the old one invalid; empty when it would not.</summary>
    public static List<string> BreakingReasons(string key, JsonObject old, JsonObject @new)
    {
        var (kind, _) = Split(key);
        var reasons = new List<string>();

        switch (kind)
        {
            case EventType:
                Narrowed(reasons, "place", old["place"], @new["place"]);
                RolesNarrowed(reasons, old["roles"] as JsonObject, @new["roles"] as JsonObject);
                Narrowed(reasons, "measures", old["measures"], @new["measures"]);
                TypeBreaks(reasons, old, @new);
                break;
            case EntityType:
                TypeBreaks(reasons, old, @new);
                break;
            case RelationType:
                Narrowed(reasons, "from", old["from"], @new["from"]);
                Narrowed(reasons, "to", old["to"], @new["to"]);
                TypeBreaks(reasons, old, @new);
                break;
            case ConclusionType:
                TypeBreaks(reasons, old, @new);
                Changed(reasons, "space", old, @new);
                break;
            case Measure:
                Changed(reasons, "unit", old, @new);
                if (IsTrue(old["signed"]) && !IsTrue(@new["signed"]))
                    reasons.Add("signed measure made unsigned");
                Raised(reasons, "min", old["min"], @new["min"]);
                Lowered(reasons, "max", old["max"], @new["max"]);
                break;
            case LogicalSpace:
                Changed(reasons, "kind", old, @new);
                Lowered(reasons, "max_depth", old["max_depth"], @new["max_depth"]);
                ClosedListNarrowed(reasons, "values", old["values"], @new["values"]);
                break;
            case SharedNamespace:
                Changed(reasons, "pattern", old, @new);
                Changed(reasons, "entity_type", old, @new);
                break;
            case Vocabulary:
                Narrowed(reasons, "values", old["values"], @new["values"]);
                break;
        }

        var known = Checked.GetValueOrDefault(kind) ?? [];
        foreach (var field in FieldNames(old, @new))
        {
            if (Descriptive.Contains(field) || known.Contains(field))
                continue;
            if (!JsonNode.DeepEquals(old[field], @new[field]))
                reasons.Add($"{field} changed");
        }

        return reasons;
    }

    private static void TypeBreaks(List<string> reasons, JsonObject old, JsonObject @new)
    {
        var oldText = Text(old["text"]) ?? "none";
        var newText = Text(@new["text"]) ?? "none";
        if (AllowsMissingText(oldText) && !AllowsMissingText(newText)
            || AllowsText(oldText) && !AllowsText(newText))
            reasons.Add($"text changed from {oldText} to {newText}");

        var oldVersioning = Text(old["versioning"]) ?? "versioned";
        var newVersioning = Text(@new["versioning"]) ?? "versioned";
        if (oldVersioning != newVersioning && newVersioning != "versioned")
            reasons.Add($"versioning changed from {oldVersioning} to {newVersioning}");

        AttributesNarrowed(reasons, old["attributes"] as JsonObject ?? [], @new["attributes"] as JsonObject ?? []);
    }

    private static bool AllowsText(string text) => text is "optional" or "required";

    private static bool AllowsMissingText(string text) => text is "none" or "optional";

    private static void RolesNarrowed(List<string> reasons, JsonObject? old, JsonObject? @new)
    {
        foreach (var (role, entityTypes) in old ?? [])
        {
            if (@new is null || !@new.TryGetPropertyValue(role, out var newEntityTypes))
                reasons.Add($"role {role} removed");
            else
                Narrowed(reasons, $"role {role}", entityTypes, newEntityTypes);
        }
    }

    private static void AttributesNarrowed(List<string> reasons, JsonObject old, JsonObject @new)
    {
        foreach (var (name, oldNode) in old)
        {
            if (!@new.TryGetPropertyValue(name, out var newNode) || newNode is not JsonObject n)
            {
                reasons.Add($"attribute {name} removed");
                continue;
            }
            if (oldNode is not JsonObject o)
                continue;

            var label = $"attribute {name}";
            Changed(reasons, "type", o, n, label);
            if (!IsTrue(o["required"]) && IsTrue(n["required"]))
                reasons.Add($"{label} became required");
            ClosedListNarrowed(reasons, $"{label} enum", o["enum"], n["enum"]);
            Changed(reasons, "pattern", o, n, label);
            Raised(reasons, $"{label} min_length", o["min_length"], n["min_length"]);
            Lowered(reasons, $"{label} max_length", o["max_length"], n["max_length"]);
            Lowered(reasons, $"{label} max_items", o["max_items"], n["max_items"]);
            Raised(reasons, $"{label} min", o["min"], n["min"]);
            Lowered(reasons, $"{label} max", o["max"], n["max"]);
        }

        foreach (var (name, newNode) in @new)
        {
            if (!old.ContainsKey(name) && IsTrue(newNode?["required"]))
                reasons.Add($"new attribute {name} is required");
        }
    }

    /// <summary>A list of allowed values lost some of them.</summary>
    private static void Narrowed(List<string> reasons, string what, JsonNode? old, JsonNode? @new)
    {
        var removed = Strings(old).Except(Strings(@new)).ToList();
        if (removed.Count > 0)
            reasons.Add($"{what} no longer allows {string.Join(", ", removed)}");
    }

    /// <summary>Like <see cref="Narrowed"/>, but a missing list means "anything", so adding one narrows.</summary>
    private static void ClosedListNarrowed(List<string> reasons, string what, JsonNode? old, JsonNode? @new)
    {
        if (@new is null)
            return;
        if (old is null)
            reasons.Add($"{what} became a closed list");
        else
            Narrowed(reasons, what, old, @new);
    }

    private static void Changed(List<string> reasons, string field, JsonObject old, JsonObject @new, string? label = null)
    {
        if (!JsonNode.DeepEquals(old[field], @new[field]))
            reasons.Add($"{(label is null ? "" : label + " ")}{field} changed from {Show(old[field])} to {Show(@new[field])}");
    }

    /// <summary>A lower bound was added or raised.</summary>
    private static void Raised(List<string> reasons, string what, JsonNode? old, JsonNode? @new)
    {
        var o = Number(old);
        var n = Number(@new);
        if (n is not null && (o is null || n > o))
            reasons.Add($"{what} raised from {Show(old)} to {Show(@new)}");
    }

    /// <summary>An upper bound was added or lowered.</summary>
    private static void Lowered(List<string> reasons, string what, JsonNode? old, JsonNode? @new)
    {
        var o = Number(old);
        var n = Number(@new);
        if (n is not null && (o is null || n < o))
            reasons.Add($"{what} lowered from {Show(old)} to {Show(@new)}");
    }

    private static IEnumerable<string> FieldNames(JsonObject a, JsonObject b) =>
        a.Select(p => p.Key).Union(b.Select(p => p.Key)).Order(StringComparer.Ordinal);

    private static string Show(JsonNode? node) => node?.ToJsonString() ?? "none";
}
