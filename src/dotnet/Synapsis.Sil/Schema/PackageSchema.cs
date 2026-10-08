using System.Text.Json.Nodes;
using Json.Schema;

namespace Synapsis.Sil.Schema;

/// <summary>
/// <c>schemas/sil/schema-package.schema.json</c>, compiled once. A malformed package is
/// refused here, before any change or reference rule runs.
/// </summary>
public sealed class PackageSchema
{
    public const string ErrorCode = "E-SCHEMA-PACKAGE";

    private readonly JsonSchema _schema;
    private readonly EvaluationOptions _options = new() { OutputFormat = OutputFormat.List };

    private PackageSchema(JsonSchema schema)
    {
        _schema = schema;
    }

    public static PackageSchema Load(string path) => FromText(File.ReadAllText(path));

    public static PackageSchema FromText(string json) => new(JsonSchema.FromText(json));

    /// <summary>Every problem in the package, in path order; empty when it is well formed.</summary>
    public IReadOnlyList<SchemaProblem> Check(JsonNode package)
    {
        var results = _schema.Evaluate(package, _options);
        return SchemaProblem.FromResults(ErrorCode, results);
    }
}
