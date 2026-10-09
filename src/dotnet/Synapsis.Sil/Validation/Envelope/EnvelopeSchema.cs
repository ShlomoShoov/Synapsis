using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Synapsis.Sil.Records;

namespace Synapsis.Sil.Validation.Envelope;

/// <summary>
/// Rule 2: the record's structure must match the record language's JSON Schema (schemas/sil/sil-1.2.schema.json):
/// required fields present, no undeclared fields, values of the right type and format. The schema is built into this
/// library at compile time and prepared once per kind.
/// </summary>
public static class EnvelopeSchema
{
    /// <summary>The name the schema file has inside this library (see Synapsis.Sil.csproj).</summary>
    private const string ResourceName = "sil-1.2.schema.json";

    /// <summary>One prepared schema per record kind.</summary>
    private static readonly Dictionary<RecordKind, JsonSchema> SchemaByKind = BuildAll();

    /// <summary>Evaluation settings: a flat list of results, so the first problem is easy to find.</summary>
    private static readonly EvaluationOptions Options = new EvaluationOptions { OutputFormat = OutputFormat.List };

    /// <summary>Returns E-SCHEMA with the location of the first problem, or null when the structure is right.</summary>
    public static RecordError? Check(RecordKind kind, JsonElement record)
    {
        EvaluationResults results = SchemaByKind[kind].Evaluate(record, Options);
        if (results.IsValid)
        {
            return null;
        }
        EvaluationResults? first = MostSpecificProblem(results);
        if (first == null)
        {
            return new RecordError(ErrorCodes.Schema, string.Empty, "the record does not match the record language");
        }
        string path = RecordPath.FromJsonPointer(first.InstanceLocation.ToString());
        return new RecordError(ErrorCodes.Schema, path, FirstMessage(first));
    }

    /// <summary>
    /// The result that best explains the problem. When part of a record fails, the 2020-12 schema rules also report
    /// every field as "not allowed" (a side effect of unevaluatedProperties), so a result that names a real check
    /// (maximum, required, type) comes first; then the one pointing deepest into the record; then the first in path
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

    /// <summary>How many steps a JSON Pointer goes into the record ("/measures/2/value" is 3).</summary>
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
    private static string FirstMessage(EvaluationResults result)
    {
        foreach (KeyValuePair<string, string> error in result.Errors!)
        {
            return error.Value;
        }
        return "the record does not match the record language";
    }

    /// <summary>Prepares the schema of each kind from the built-in schema file.</summary>
    private static Dictionary<RecordKind, JsonSchema> BuildAll()
    {
        JsonNode definitions = ReadSchemaFile()["$defs"]!;
        Dictionary<RecordKind, JsonSchema> schemas = new Dictionary<RecordKind, JsonSchema>();
        schemas[RecordKind.Entity] = BuildFor("entity", definitions);
        schemas[RecordKind.Event] = BuildFor("event", definitions);
        schemas[RecordKind.Relation] = BuildFor("relation", definitions);
        schemas[RecordKind.Conclusion] = BuildFor("conclusion", definitions);
        return schemas;
    }

    /// <summary>A schema that checks one kind: the file's definitions, pointing at that kind's definition.</summary>
    private static JsonSchema BuildFor(string kind, JsonNode definitions)
    {
        JsonObject schema = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$defs"] = definitions.DeepClone(),
            ["$ref"] = "#/$defs/" + kind,
        };
        Uri address = new Uri("https://synapsis.local/schemas/sil-1.2/" + kind);
        return JsonSchema.FromText(schema.ToJsonString(), new BuildOptions(), address);
    }

    /// <summary>Reads the schema file built into this library.</summary>
    private static JsonNode ReadSchemaFile()
    {
        Assembly library = typeof(EnvelopeSchema).Assembly;
        using Stream? stream = library.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"The built-in file '{ResourceName}' is missing from Synapsis.Sil.");
        }
        JsonNode? schema = JsonNode.Parse(stream);
        if (schema == null)
        {
            throw new InvalidOperationException($"The built-in file '{ResourceName}' is empty.");
        }
        return schema;
    }
}
