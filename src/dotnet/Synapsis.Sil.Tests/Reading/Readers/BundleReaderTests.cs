using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Reading.Readers;

/// <summary>
/// The request body as a whole: a bundle of at most 5,000 records with a valid envelope. The refused cases are the
/// request cases of tests/conformance/cases/07-requests.json that do not depend on HTTP.
/// </summary>
public class BundleReaderTests
{
    /// <summary>The bundle reader, through its abstraction.</summary>
    private static readonly IBundleReader Reader = SilComponents.CreateBundleReader();

    /// <summary>A bundle reads with its envelope fields, and its records stay raw JSON.</summary>
    [Fact]
    public void A_bundle_reads_and_keeps_its_records_raw()
    {
        IngestBundle bundle = Reader.Read(Bundle(new JsonObject())).Value!;
        Assert.Equal("1.2", bundle.Sil);
        Assert.Equal("test-batch-0001", bundle.BatchId);
        Assert.Equal("tests", bundle.Producer);
        Assert.Single(bundle.Records);
        Assert.Equal(JsonValueKind.Object, bundle.Records[0].ValueKind);
    }

    /// <summary>Text that is not JSON, or JSON with NaN, is not a bundle.</summary>
    [Theory]
    [InlineData("{\"sil\": ")]
    [InlineData("{\"sil\": \"1.2\", \"batch_id\": \"nan-0001\", \"producer\": \"tests\", \"records\": [{\"reliability\": NaN}]}")]
    public void A_body_that_is_not_JSON_is_refused(string body)
    {
        Assert.Equal(ErrorCodes.Bundle, Reader.Read(body).Problem!.Code);
    }

    /// <summary>A wrong envelope is refused as a whole: another SIL version, no records, a batch id with spaces, a record that is not an object.</summary>
    [Theory]
    [InlineData("sil", "\"1.1\"")]
    [InlineData("records", "[]")]
    [InlineData("batch_id", "\"my batch\"")]
    [InlineData("records", "[\"evt:usgs:x\"]")]
    public void A_wrong_envelope_is_refused(string field, string value)
    {
        JsonObject changes = new JsonObject { [field] = JsonNode.Parse(value) };
        Assert.Equal(ErrorCodes.Bundle, Reader.Read(Bundle(changes)).Problem!.Code);
    }

    /// <summary>More than 5,000 records is E-TOO-LARGE, although the envelope's schema would also refuse it.</summary>
    [Fact]
    public void More_than_five_thousand_records_is_too_large()
    {
        JsonArray records = new JsonArray();
        for (int index = 0; index < 5001; index++)
        {
            records.Add(TestRecords.Earthquake());
        }
        Result<IngestBundle> result = Reader.Read(Bundle(new JsonObject { ["records"] = records }));
        Assert.Equal(ErrorCodes.TooLarge, result.Problem!.Code);
    }

    /// <summary>A valid bundle holding one earthquake, with the given fields replaced.</summary>
    private static string Bundle(JsonObject changes)
    {
        JsonObject bundle = new JsonObject
        {
            ["sil"] = "1.2",
            ["batch_id"] = "test-batch-0001",
            ["producer"] = "tests",
            ["records"] = new JsonArray(TestRecords.Earthquake()),
        };
        foreach (KeyValuePair<string, JsonNode?> change in changes)
        {
            bundle[change.Key] = change.Value?.DeepClone();
        }
        return bundle.ToJsonString();
    }
}
