using System.Text.Json.Nodes;

namespace Synapsis.Api.Tests.Support;

/// <summary>Batches built from the valid records of tests/conformance/cases/01-valid.json.</summary>
public static class TestBatches
{
    /// <summary>The records of one valid case, as changeable JSON.</summary>
    public static JsonArray ValidCase(int caseIndex)
    {
        JsonNode file = JsonNode.Parse(RepositoryFiles.ReadText("tests/conformance/cases/01-valid.json"))!;
        return file["cases"]![caseIndex]!["records"]!.DeepClone().AsArray();
    }

    /// <summary>A bundle holding these records, as the request body.</summary>
    public static string Body(JsonArray records)
    {
        JsonObject bundle = new JsonObject
        {
            ["sil"] = "1.2",
            ["batch_id"] = "test-batch-0001",
            ["producer"] = "tests",
            ["records"] = records.DeepClone(),
        };
        return bundle.ToJsonString();
    }
}
