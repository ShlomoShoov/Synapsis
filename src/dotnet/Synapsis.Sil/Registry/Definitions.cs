using System.Text.Json.Nodes;

namespace Synapsis.Sil.Registry;

/// <summary>
/// Turns a schema package into <c>'&lt;kind&gt;:&lt;code&gt;'</c> definitions, the unit the
/// registry stores, diffs and owns. Defaults are filled in so that equal meaning compares equal.
/// </summary>
public static class Definitions
{
    public const string Source = "source";
    public const string RecordNamespace = "record_namespace";
    public const string EntityNamespace = "entity_namespace";
    public const string SharedNamespace = "shared_namespace";
    public const string EntityType = "entity_type";
    public const string EventType = "event_type";
    public const string RelationType = "relation_type";
    public const string ConclusionType = "conclusion_type";
    public const string Role = "role";
    public const string Measure = "measure";
    public const string LogicalSpace = "logical_space";
    public const string Vocabulary = "vocabulary";

    public static string Key(string kind, string code) => $"{kind}:{code}";

    /// <summary><c>source:src:cisa:kev</c> splits into <c>source</c> and <c>src:cisa:kev</c>.</summary>
    public static (string Kind, string Code) Split(string key)
    {
        var colon = key.IndexOf(':');
        if (colon <= 0)
            throw new FormatException($"'{key}' is not a '<kind>:<code>' key");
        return (key[..colon], key[(colon + 1)..]);
    }

    /// <summary>
    /// Every definition the package makes, keyed <c>'&lt;kind&gt;:&lt;code&gt;'</c>. The package
    /// must already have passed <see cref="Schema.PackageSchema"/>; a code defined twice in
    /// one package is a <see cref="FormatException"/>.
    /// </summary>
    public static Dictionary<string, JsonObject> Flatten(JsonObject package)
    {
        var defs = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        if (package["namespaces"] is JsonObject namespaces)
        {
            foreach (var ns in Strings(namespaces["records"]))
                Add(defs, RecordNamespace, ns, new JsonObject());
            foreach (var ns in Strings(namespaces["entities"]))
                Add(defs, EntityNamespace, ns, new JsonObject());
        }

        AddList(defs, package, "sources", Source, "id", _ => { });
        AddList(defs, package, "shared_namespaces", SharedNamespace, "namespace", _ => { });
        AddList(defs, package, "entity_types", EntityType, "code", TypeDefaults);
        AddList(defs, package, "event_types", EventType, "code", d =>
        {
            TypeDefaults(d);
            Default(d, "place", new JsonArray());
            Default(d, "roles", new JsonObject());
            Default(d, "measures", new JsonArray());
        });
        AddList(defs, package, "relation_types", RelationType, "code", d =>
        {
            TypeDefaults(d);
            Default(d, "from", new JsonArray());
            Default(d, "to", new JsonArray());
        });
        AddList(defs, package, "conclusion_types", ConclusionType, "code", TypeDefaults);
        AddList(defs, package, "roles", Role, "code", _ => { });
        AddList(defs, package, "measures", Measure, "code", d => Default(d, "signed", false));
        AddList(defs, package, "logical_spaces", LogicalSpace, "space", d => Default(d, "kind", "tree"));

        if (package["vocabularies"] is JsonObject vocabularies)
        {
            foreach (var (name, values) in vocabularies)
                Add(defs, Vocabulary, name, new JsonObject { ["values"] = values?.DeepClone() });
        }

        return defs;
    }

    /// <summary>The keys of the definitions marked <c>deprecated: true</c>.</summary>
    public static HashSet<string> DeprecatedKeys(IReadOnlyDictionary<string, JsonObject> defs) =>
        defs.Where(d => IsTrue(d.Value["deprecated"])).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    private static void AddList(
        Dictionary<string, JsonObject> defs, JsonObject package, string section, string kind, string codeField,
        Action<JsonObject> fillDefaults)
    {
        if (package[section] is not JsonArray items)
            return;
        foreach (var item in items.OfType<JsonObject>())
        {
            var code = Text(item[codeField])
                ?? throw new FormatException($"an item in '{section}' has no '{codeField}'");
            var definition = item.DeepClone().AsObject();
            fillDefaults(definition);
            Add(defs, kind, code, definition);
        }
    }

    private static void Add(Dictionary<string, JsonObject> defs, string kind, string code, JsonObject definition)
    {
        Default(definition, "deprecated", false);
        if (!defs.TryAdd(Key(kind, code), definition))
            throw new FormatException($"{Key(kind, code)} is defined twice in the package");
    }

    private static void TypeDefaults(JsonObject d)
    {
        Default(d, "text", "none");
        Default(d, "versioning", "versioned");
        Default(d, "attributes", new JsonObject());
        foreach (var (_, attribute) in d["attributes"]!.AsObject())
        {
            if (attribute is JsonObject a)
                Default(a, "required", false);
        }
    }

    private static void Default(JsonObject d, string field, JsonNode? value)
    {
        if (!d.ContainsKey(field))
            d[field] = value;
    }

    internal static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array ? array.Select(Text).OfType<string>() : [];

    internal static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    internal static bool IsTrue(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    internal static double? Number(JsonNode? node)
    {
        if (node is not JsonValue v)
            return null;
        if (v.TryGetValue<double>(out var d))
            return d;
        if (v.TryGetValue<long>(out var l))
            return l;
        if (v.TryGetValue<int>(out var i))
            return i;
        if (v.TryGetValue<decimal>(out var m))
            return (double)m;
        return null;
    }
}
