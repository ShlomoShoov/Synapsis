using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Descriptions;
using Synapsis.Sil.Services.Schemas;
namespace Synapsis.Sil.Services.Rules.Envelope;

/// <summary>
/// Rule 2: the record's structure must match the record language's JSON Schema (schemas/sil/sil-1.2.schema.json):
/// required fields present, no undeclared fields, values of the right type and format. The schema is built into this
/// library at compile time and prepared once per kind. Runs after rule 1, so the kind is known to be valid.
/// </summary>
public sealed class StructureRule : IEnvelopeRule
{
    /// <summary>One prepared schema per record kind.</summary>
    private static readonly Dictionary<string, JsonSchema> SchemaByKind = BuildAll();

    /// <summary>Passes when the structure is right; E-SCHEMA at the location that best explains the problem otherwise.</summary>
    public Result Check(JsonElement raw)
    {
        return SchemaEvaluation.Check(SchemaByKind[KindText.Of(raw)], raw, ErrorCodes.Schema, "the record does not match the record language");
    }

    /// <summary>Prepares the schema of each kind from the built-in schema file.</summary>
    private static Dictionary<string, JsonSchema> BuildAll()
    {
        JsonNode definitions = DescriptionFiles.ReadJson(DescriptionFiles.RecordSchema)["$defs"]!;
        Dictionary<string, JsonSchema> schemas = new Dictionary<string, JsonSchema>();
        schemas["entity"] = BuildFor("entity", definitions);
        schemas["event"] = BuildFor("event", definitions);
        schemas["relation"] = BuildFor("relation", definitions);
        schemas["conclusion"] = BuildFor("conclusion", definitions);
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
}
