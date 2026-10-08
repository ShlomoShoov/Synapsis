using System.Collections.Frozen;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Synapsis.Sil.Records;
using static Synapsis.Sil.Registry.Definitions;

namespace Synapsis.Sil.Registry;

public sealed record SourceInfo(
    string Id,
    string Package,
    double Reliability,
    string? ReliabilityBasis,
    int PublicationLagDays);

/// <summary>An entity, event, relation or conclusion type. Fields that do not apply to a kind are empty.</summary>
public sealed record TypeInfo(
    RecordKind Kind,
    string Code,
    string Package,
    string Text,
    string Versioning,
    IReadOnlySet<string> PlaceForms,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Roles,
    IReadOnlySet<string> Measures,
    IReadOnlySet<string> From,
    IReadOnlySet<string> To,
    string? Space,
    JsonObject Definition);

public sealed record MeasureInfo(string Code, string? Unit, bool Signed, double? Min, double? Max);

/// <summary><see cref="Values"/> is null for an open space.</summary>
public sealed record SpaceInfo(string Space, string Kind, int? MaxDepth, IReadOnlySet<string>? Values);

/// <summary>An authority namespace any package may use, e.g. CVE ids or IMO numbers.</summary>
public sealed record SharedNamespaceInfo(string Namespace, Regex Pattern, string? EntityType);

/// <summary>
/// One registry revision compiled into lookup tables. Immutable: a request keeps the
/// snapshot it started with, even if the registry moves on meanwhile.
/// </summary>
public sealed class RegistrySnapshot
{
    private static readonly Dictionary<string, RecordKind> TypeKinds = new()
    {
        [EntityType] = RecordKind.Entity,
        [EventType] = RecordKind.Event,
        [RelationType] = RecordKind.Relation,
        [ConclusionType] = RecordKind.Conclusion,
    };

    private readonly FrozenSet<string> _deprecated;

    public long Revision { get; }

    /// <summary>Source id to its registered reliability, basis and publication lag.</summary>
    public IReadOnlyDictionary<string, SourceInfo> Sources { get; }

    /// <summary>Source id to the package that owns it.</summary>
    public IReadOnlyDictionary<string, string> SourcePackage { get; }

    /// <summary>Record namespace (<c>evt:kev:…</c>) to the package that owns it.</summary>
    public IReadOnlyDictionary<string, string> RecordNamespaces { get; }

    /// <summary>Entity namespace (<c>ent:kev-vendor:…</c>) to the package that owns it.</summary>
    public IReadOnlyDictionary<string, string> EntityNamespaces { get; }

    public IReadOnlyDictionary<string, SharedNamespaceInfo> SharedNamespaces { get; }

    /// <summary>Per record kind, type code to its definition.</summary>
    public IReadOnlyDictionary<RecordKind, IReadOnlyDictionary<string, TypeInfo>> Types { get; }

    public IReadOnlySet<string> Roles { get; }

    public IReadOnlyDictionary<string, MeasureInfo> Measures { get; }

    public IReadOnlyDictionary<string, SpaceInfo> Spaces { get; }

    /// <summary>Per (kind, type), attribute name to its compiled rule.</summary>
    public IReadOnlyDictionary<(RecordKind Kind, string Type), IReadOnlyDictionary<string, AttributeRule>> Attributes { get; }

    /// <summary>The closed lists kept in <c>core</c>: <c>precision</c>, <c>known_at_basis</c>, <c>reliability_basis</c>.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Vocabularies { get; }

    public RegistrySnapshot(long revision, IEnumerable<RegistryEntry> entries)
    {
        Revision = revision;

        var deprecated = new HashSet<string>(StringComparer.Ordinal);
        var sources = new Dictionary<string, SourceInfo>(StringComparer.Ordinal);
        var recordNamespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        var entityNamespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        var sharedNamespaces = new Dictionary<string, SharedNamespaceInfo>(StringComparer.Ordinal);
        var types = Enum.GetValues<RecordKind>()
            .ToDictionary(k => k, _ => new Dictionary<string, TypeInfo>(StringComparer.Ordinal));
        var roles = new HashSet<string>(StringComparer.Ordinal);
        var measures = new Dictionary<string, MeasureInfo>(StringComparer.Ordinal);
        var spaces = new Dictionary<string, SpaceInfo>(StringComparer.Ordinal);
        var attributes = new Dictionary<(RecordKind, string), IReadOnlyDictionary<string, AttributeRule>>();
        var vocabularies = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var (kind, code) = Split(entry.Key);
            var d = entry.Definition;
            if (entry.Deprecated)
                deprecated.Add(entry.Key);

            switch (kind)
            {
                case Source:
                    sources[code] = new SourceInfo(
                        code,
                        entry.Package,
                        Number(d["reliability"]) ?? 0,
                        Text(d["reliability_basis"]),
                        (int)(Number(d["publication_lag_days"]) ?? 0));
                    break;
                case RecordNamespace:
                    recordNamespaces[code] = entry.Package;
                    break;
                case EntityNamespace:
                    entityNamespaces[code] = entry.Package;
                    break;
                case SharedNamespace:
                    sharedNamespaces[code] = new SharedNamespaceInfo(
                        code,
                        new Regex(Text(d["pattern"]) ?? ".*", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
                        Text(d["entity_type"]));
                    break;
                case EntityType or EventType or RelationType or ConclusionType:
                    var recordKind = TypeKinds[kind];
                    types[recordKind][code] = BuildType(recordKind, code, entry.Package, d);
                    attributes[(recordKind, code)] = (d["attributes"] as JsonObject ?? [])
                        .Where(a => a.Value is JsonObject)
                        .ToFrozenDictionary(a => a.Key, a => AttributeRule.Compile(a.Value!.AsObject()), StringComparer.Ordinal);
                    break;
                case Role:
                    roles.Add(code);
                    break;
                case Definitions.Measure:
                    measures[code] = new MeasureInfo(
                        code, Text(d["unit"]), IsTrue(d["signed"]), Number(d["min"]), Number(d["max"]));
                    break;
                case LogicalSpace:
                    spaces[code] = new SpaceInfo(
                        code,
                        Text(d["kind"]) ?? "tree",
                        (int?)Number(d["max_depth"]),
                        d["values"] is JsonArray values ? Set(values) : null);
                    break;
                case Vocabulary:
                    vocabularies[code] = Set(d["values"]);
                    break;
            }
        }

        _deprecated = deprecated.ToFrozenSet(StringComparer.Ordinal);
        Sources = sources.ToFrozenDictionary(StringComparer.Ordinal);
        SourcePackage = sources.ToFrozenDictionary(s => s.Key, s => s.Value.Package, StringComparer.Ordinal);
        RecordNamespaces = recordNamespaces.ToFrozenDictionary(StringComparer.Ordinal);
        EntityNamespaces = entityNamespaces.ToFrozenDictionary(StringComparer.Ordinal);
        SharedNamespaces = sharedNamespaces.ToFrozenDictionary(StringComparer.Ordinal);
        Types = types.ToFrozenDictionary(
            t => t.Key, IReadOnlyDictionary<string, TypeInfo> (t) => t.Value.ToFrozenDictionary(StringComparer.Ordinal));
        Roles = roles.ToFrozenSet(StringComparer.Ordinal);
        Measures = measures.ToFrozenDictionary(StringComparer.Ordinal);
        Spaces = spaces.ToFrozenDictionary(StringComparer.Ordinal);
        Attributes = attributes.ToFrozenDictionary();
        Vocabularies = vocabularies.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>True when the definition <c>'&lt;kind&gt;:&lt;code&gt;'</c> is marked deprecated.</summary>
    public bool IsDeprecated(string key) => _deprecated.Contains(key);

    private static TypeInfo BuildType(RecordKind kind, string code, string package, JsonObject d) => new(
        kind,
        code,
        package,
        Text(d["text"]) ?? "none",
        Text(d["versioning"]) ?? "versioned",
        Set(d["place"]),
        (d["roles"] as JsonObject ?? []).ToFrozenDictionary(
            r => r.Key, IReadOnlySet<string> (r) => Set(r.Value), StringComparer.Ordinal),
        Set(d["measures"]),
        Set(d["from"]),
        Set(d["to"]),
        Text(d["space"]),
        d);

    private static FrozenSet<string> Set(JsonNode? node) => Strings(node).ToFrozenSet(StringComparer.Ordinal);
}
