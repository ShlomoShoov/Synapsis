using System.Text.Json;
using Json.Schema;
using Synapsis.Sil.Records;

namespace Synapsis.Sil.Schema;

/// <summary>
/// <c>schemas/sil/sil-1.2.schema.json</c>, compiled once. Checks the fixed envelope: the
/// bundle, and each record against the definition of its kind (<c>$defs/&lt;kind&gt;</c>).
/// </summary>
public sealed class EnvelopeSchema
{
    public const string ErrorCode = "E-SCHEMA";

    private readonly JsonSchema _bundle;
    private readonly Dictionary<RecordKind, JsonSchema> _records;
    private readonly EvaluationOptions _options;

    private EnvelopeSchema(JsonSchema bundle)
    {
        _bundle = bundle;
        _options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        _options.SchemaRegistry.Register(bundle);
        _records = Enum.GetValues<RecordKind>().ToDictionary(
            kind => kind,
            kind => new JsonSchemaBuilder()
                .Ref(new Uri(bundle.BaseUri, $"#/$defs/{KindName(kind)}"))
                .Build());
    }

    public static EnvelopeSchema Load(string path) => FromText(File.ReadAllText(path));

    public static EnvelopeSchema FromText(string json) => new(JsonSchema.FromText(json));

    /// <summary>The first problem in the bundle, in path order, or null.</summary>
    public SchemaProblem? CheckBundle(JsonElement bundle) => First(_bundle, bundle);

    /// <summary>The first problem in one record, in path order, or null.</summary>
    public SchemaProblem? CheckRecord(RecordKind kind, JsonElement record) => First(_records[kind], record);

    public static string KindName(RecordKind kind) => kind switch
    {
        RecordKind.Entity => "entity",
        RecordKind.Event => "event",
        RecordKind.Relation => "relation",
        RecordKind.Conclusion => "conclusion",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private SchemaProblem? First(JsonSchema schema, JsonElement instance)
    {
        var results = schema.Evaluate(instance, _options);
        return SchemaProblem.FromResults(ErrorCode, results).FirstOrDefault();
    }
}
