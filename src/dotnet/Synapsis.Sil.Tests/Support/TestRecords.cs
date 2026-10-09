using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Synapsis.Sil.Tests.Support;

/// <summary>
/// Valid records taken from the conformance file 01-valid.json, so tests start from records the contract accepts and
/// change one thing at a time. Each call returns a fresh copy the test may change.
/// </summary>
public static class TestRecords
{
    /// <summary>An earthquake: a point with its uncertainty, two measures, an attribute (case 0).</summary>
    public static JsonObject Earthquake()
    {
        return ValidCase(0)[0];
    }

    /// <summary>A KEV entry: vulnerability, vendor, product, PRODUCT_OF, the catalog event (case 1).</summary>
    public static List<JsonObject> KevEntry()
    {
        return ValidCase(1);
    }

    /// <summary>A KEV entry plus a classification conclusion about its product (case 4).</summary>
    public static List<JsonObject> KevEntryWithClassification()
    {
        return ValidCase(4);
    }

    /// <summary>A bank's quarterly report: a logical and an admin place, FDIC measures (case 3, the event).</summary>
    public static JsonObject BankQuarter()
    {
        return ValidCase(3)[1];
    }

    /// <summary>The KEV catalog event on its own (case 1, the event).</summary>
    public static JsonObject KevEvent()
    {
        return ValidCase(1)[4];
    }

    /// <summary>The CVE entity in the shared cve namespace (case 1, the first record).</summary>
    public static JsonObject CveEntity()
    {
        return ValidCase(1)[0];
    }

    /// <summary>The product-class conclusion on its own (case 4, the last record).</summary>
    public static JsonObject Classification()
    {
        return ValidCase(4)[5];
    }

    /// <summary>The records of one case of 01-valid.json, as changeable JSON objects.</summary>
    public static List<JsonObject> ValidCase(int caseIndex)
    {
        JsonNode file = JsonNode.Parse(RepositoryFiles.ReadText("tests/conformance/cases/01-valid.json"))!;
        JsonArray records = file["cases"]![caseIndex]!["records"]!.AsArray();
        List<JsonObject> copies = new List<JsonObject>();
        foreach (JsonNode? record in records)
        {
            copies.Add(record!.DeepClone().AsObject());
        }
        return copies;
    }

    /// <summary>A JSON object as a read-only JSON element, the form the validator takes.</summary>
    public static JsonElement ToElement(JsonNode record)
    {
        return JsonSerializer.SerializeToElement(record);
    }
}
