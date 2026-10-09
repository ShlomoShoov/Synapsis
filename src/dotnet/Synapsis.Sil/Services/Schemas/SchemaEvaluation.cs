using System.Collections.Generic;
using System.Text.Json;
using Json.Schema;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Services.Schemas;

/// <summary>
/// Runs a JSON Schema check and reports its most telling problem as one <see cref="Problem"/>, with the path in the
/// contract's format ("measures[2].value"). Used for records (rule 2), bundles and schema packages.
/// </summary>
public static class SchemaEvaluation
{
    /// <summary>Evaluation settings: a flat list of results, so every problem is easy to find.</summary>
    private static readonly EvaluationOptions Options = new EvaluationOptions { OutputFormat = OutputFormat.List };

    /// <summary>Passes when the value matches the schema; otherwise fails with this code at the most specific problem.</summary>
    public static Result Check(JsonSchema schema, JsonElement value, string code, string generalMessage)
    {
        EvaluationResults results = schema.Evaluate(value, Options);
        if (results.IsValid)
        {
            return Result.Success();
        }
        EvaluationResults? best = MostSpecificProblem(results);
        if (best == null)
        {
            return Result.Failure(code, string.Empty, generalMessage);
        }
        string path = RecordPath.FromJsonPointer(best.InstanceLocation.ToString());
        return Result.Failure(code, path, FirstMessage(best, generalMessage));
    }

    /// <summary>
    /// The result that best explains the problem. When part of a value fails, the 2020-12 schema rules also report
    /// every field as "not allowed" (a side effect of unevaluatedProperties), so a result that names a real check
    /// (maximum, required, type) comes first; then the one pointing deepest into the value; then the first in path
    /// order, so the choice is the same every time.
    /// </summary>
    private static EvaluationResults? MostSpecificProblem(EvaluationResults results)
    {
        EvaluationResults? best = null;
        if (results.Details == null)
        {
            return null;
        }
        foreach (EvaluationResults detail in results.Details)
        {
            if (detail.Errors == null || detail.Errors.Count == 0)
            {
                continue;
            }
            if (best == null || IsBetter(detail, best))
            {
                best = detail;
            }
        }
        return best;
    }

    /// <summary>True when <paramref name="candidate"/> explains the problem better than <paramref name="current"/>.</summary>
    private static bool IsBetter(EvaluationResults candidate, EvaluationResults current)
    {
        bool candidateNamesCheck = NamesACheck(candidate);
        bool currentNamesCheck = NamesACheck(current);
        if (candidateNamesCheck != currentNamesCheck)
        {
            return candidateNamesCheck;
        }
        string candidateLocation = candidate.InstanceLocation.ToString();
        string currentLocation = current.InstanceLocation.ToString();
        int candidateDepth = Depth(candidateLocation);
        int currentDepth = Depth(currentLocation);
        if (candidateDepth != currentDepth)
        {
            return candidateDepth > currentDepth;
        }
        return string.CompareOrdinal(candidateLocation, currentLocation) < 0;
    }

    /// <summary>
    /// True when the result names a schema check (its error key is a keyword such as "maximum"); false for the bare
    /// "this field is not allowed" results, whose key is empty.
    /// </summary>
    private static bool NamesACheck(EvaluationResults result)
    {
        foreach (string keyword in result.Errors!.Keys)
        {
            if (keyword.Length > 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>How many steps a JSON Pointer goes into the value ("/measures/2/value" is 3).</summary>
    private static int Depth(string pointer)
    {
        int depth = 0;
        foreach (char character in pointer)
        {
            if (character == '/')
            {
                depth++;
            }
        }
        return depth;
    }

    /// <summary>The first error message of a result.</summary>
    private static string FirstMessage(EvaluationResults result, string generalMessage)
    {
        foreach (KeyValuePair<string, string> error in result.Errors!)
        {
            return error.Value;
        }
        return generalMessage;
    }
}
