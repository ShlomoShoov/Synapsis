using System.Text;
using Json.Pointer;
using Json.Schema;

namespace Synapsis.Sil.Schema;

/// <summary>One JSON Schema failure: the error code, the JSON path it points at, and why.</summary>
public sealed record SchemaProblem(string Code, string Path, string Message)
{
    /// <summary>Every failing location in <paramref name="results"/>, in path order.</summary>
    internal static List<SchemaProblem> FromResults(string code, EvaluationResults results)
    {
        if (results.IsValid)
            return [];

        var problems = new List<SchemaProblem>();
        foreach (var detail in results.Details.Prepend(results))
        {
            if (detail.Errors is null)
                continue;
            var path = ToJsonPath(detail.InstanceLocation);
            foreach (var message in detail.Errors.Values)
                problems.Add(new SchemaProblem(code, path, message));
        }
        if (problems.Count == 0)
            problems.Add(new SchemaProblem(code, "$", "does not match the schema"));

        return problems
            .DistinctBy(p => (p.Path, p.Message))
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary><c>/records/0/time</c> becomes <c>$.records[0].time</c>.</summary>
    internal static string ToJsonPath(JsonPointer pointer)
    {
        var path = new StringBuilder("$");
        foreach (var raw in pointer.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = raw.Replace("~1", "/").Replace("~0", "~");
            if (int.TryParse(segment, out var index))
                path.Append('[').Append(index).Append(']');
            else
                path.Append('.').Append(segment);
        }
        return path.ToString();
    }
}
