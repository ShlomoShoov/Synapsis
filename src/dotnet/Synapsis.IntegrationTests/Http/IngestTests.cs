using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Synapsis.IntegrationTests.Support;
using Xunit;

namespace Synapsis.IntegrationTests.Http;

/// <summary>POST /ingest and GET /records through the whole server, against MongoDB: the write path and versions.</summary>
public sealed class IngestTests : IDisposable
{
    private readonly ApiServer _server = new ApiServer();

    /// <summary>Stops the server and deletes its database.</summary>
    public void Dispose()
    {
        _server.Dispose();
    }

    /// <summary>Stored, sent again (unchanged), changed (version 2); a read shows each version as known at its date.</summary>
    [Fact]
    public async Task A_record_is_stored_versioned_and_read_as_of_a_date()
    {
        await RegisterUsgsAsync();
        Assert.Equal("accepted", await StatusOfAsync(Quake(7.5)));
        Assert.Equal("unchanged", await StatusOfAsync(Quake(7.5)));
        JsonElement changed = await ReceiptOfAsync(Quake(7.6));
        Assert.Equal(1, changed.GetProperty("counts").GetProperty("versioned").GetInt32());

        JsonElement latest = await ReadAsync("");
        Assert.Equal(2, latest.GetProperty("statements")[0].GetProperty("version").GetInt32());
        Assert.Equal("ingested", latest.GetProperty("statements")[0].GetProperty("known_at_basis").GetString());
        JsonElement in2025 = await ReadAsync("?as_of=2025-01-01");
        Assert.Equal(1, in2025.GetProperty("statements")[0].GetProperty("version").GetInt32());
    }

    /// <summary>Re-reading the past (version 1's content with its old known_at) after version 2 is unchanged.</summary>
    [Fact]
    public async Task Re_reading_the_past_is_unchanged()
    {
        await RegisterUsgsAsync();
        await StatusOfAsync(Quake(7.5));
        await StatusOfAsync(Quake(7.6));
        Assert.Equal("unchanged", await StatusOfAsync(Quake(7.5)));
    }

    /// <summary>A dry run answers the receipt and stores nothing.</summary>
    [Fact]
    public async Task A_dry_run_stores_nothing()
    {
        await RegisterUsgsAsync();
        HttpResponseMessage response = await _server.IngestAsync(Quake(7.5), true);
        JsonElement receipt = await ApiServer.JsonOf(response);
        Assert.True(receipt.GetProperty("dry_run").GetBoolean());
        Assert.Equal(1, receipt.GetProperty("counts").GetProperty("new").GetInt32());
        HttpResponseMessage read = await _server.Client.GetAsync("/records/" + QuakeId());
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("E-NOT-FOUND", (await ApiServer.JsonOf(read)).GetProperty("code").GetString());
    }

    /// <summary>A body announced as gzip that is not gzip is 400 E-BUNDLE.</summary>
    [Fact]
    public async Task A_body_announced_as_gzip_that_is_not_is_refused()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/ingest");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Content.Headers.ContentEncoding.Add("gzip");
        request.Headers.Add("X-Api-Key", ApiServer.Key);
        HttpResponseMessage response = await _server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("E-BUNDLE", (await ApiServer.JsonOf(response)).GetProperty("code").GetString());
    }

    /// <summary>A batch without the key is 401 E-AUTH.</summary>
    [Fact]
    public async Task A_batch_without_the_key_is_unauthorized()
    {
        HttpResponseMessage response = await _server.Client.PostAsync("/ingest", new StringContent(Quake(7.5), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Registers the earthquake package (core is seeded at start-up).</summary>
    private async Task RegisterUsgsAsync()
    {
        HttpResponseMessage response = await _server.PutSchemaAsync("usgs", RepositoryFiles.ReadText("feeders/packages/usgs.yaml"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>The receipt of a batch the server must accept.</summary>
    private async Task<JsonElement> ReceiptOfAsync(string body)
    {
        HttpResponseMessage response = await _server.IngestAsync(body, false);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await ApiServer.JsonOf(response);
    }

    /// <summary>The receipt's status.</summary>
    private async Task<string?> StatusOfAsync(string body)
    {
        return (await ReceiptOfAsync(body)).GetProperty("status").GetString();
    }

    /// <summary>GET /records/{the quake} with a query.</summary>
    private async Task<JsonElement> ReadAsync(string query)
    {
        HttpResponseMessage response = await _server.Client.GetAsync("/records/" + QuakeId() + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiServer.JsonOf(response);
    }

    /// <summary>A batch holding the conformance earthquake with this magnitude (its known_at stays 2024).</summary>
    private static string Quake(double magnitude)
    {
        JsonObject quake = QuakeRecord();
        quake["measures"]![0]!["value"] = magnitude;
        JsonObject bundle = new JsonObject
        {
            ["sil"] = "1.2",
            ["batch_id"] = "quake-" + magnitude.ToString(CultureInfo.InvariantCulture),
            ["producer"] = "tests",
            ["records"] = new JsonArray(quake),
        };
        return bundle.ToJsonString();
    }

    /// <summary>The earthquake's id.</summary>
    private static string QuakeId()
    {
        return QuakeRecord()["id"]!.GetValue<string>();
    }

    /// <summary>The earthquake of 01-valid.json, case 0.</summary>
    private static JsonObject QuakeRecord()
    {
        JsonNode file = JsonNode.Parse(RepositoryFiles.ReadText("tests/conformance/cases/01-valid.json"))!;
        return file["cases"]![0]!["records"]![0]!.DeepClone().AsObject();
    }
}
