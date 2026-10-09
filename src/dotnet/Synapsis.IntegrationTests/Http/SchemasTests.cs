using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.IntegrationTests.Support;
using Xunit;

namespace Synapsis.IntegrationTests.Http;

/// <summary>PUT /schemas/{package} through the whole server, against MongoDB.</summary>
public sealed class SchemasTests : IDisposable
{
    private readonly ApiServer _server = new ApiServer();

    /// <summary>Stops the server and deletes its database.</summary>
    public void Dispose()
    {
        _server.Dispose();
    }

    /// <summary>Start-up seeds core into the server's own database: sending core again is unchanged, version 1.</summary>
    [Fact]
    public async Task Core_is_seeded_at_start_up()
    {
        HttpResponseMessage response = await _server.PutSchemaAsync("core", RepositoryFiles.ReadText("schemas/seed/core.yaml"));
        JsonElement body = await ApiServer.JsonOf(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("unchanged", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        IMongoDatabase database = new MongoClient(TestDatabase.Url).GetDatabase(_server.DatabaseName);
        Assert.Equal(1, await database.GetCollection<BsonDocument>("schema_packages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    /// <summary>A new package is created (201); sent again it is unchanged (200).</summary>
    [Fact]
    public async Task A_new_package_is_created_then_unchanged()
    {
        string usgs = RepositoryFiles.ReadText("feeders/packages/usgs.yaml");
        HttpResponseMessage first = await _server.PutSchemaAsync("usgs", usgs);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("created", (await ApiServer.JsonOf(first)).GetProperty("status").GetString());
        HttpResponseMessage again = await _server.PutSchemaAsync("usgs", usgs);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("unchanged", (await ApiServer.JsonOf(again)).GetProperty("status").GetString());
    }

    /// <summary>A breaking change is 409 E-SCHEMA-BREAKING.</summary>
    [Fact]
    public async Task A_breaking_change_is_a_conflict()
    {
        string usgs = RepositoryFiles.ReadText("feeders/packages/usgs.yaml");
        await _server.PutSchemaAsync("usgs", usgs);
        HttpResponseMessage response = await _server.PutSchemaAsync("usgs", usgs.Replace("unit: km,", "unit: m,"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("E-SCHEMA-BREAKING", (await ApiServer.JsonOf(response)).GetProperty("code").GetString());
    }

    /// <summary>A package sent under another name is 422 E-SCHEMA-PACKAGE.</summary>
    [Fact]
    public async Task A_package_under_another_name_is_unprocessable()
    {
        HttpResponseMessage response = await _server.PutSchemaAsync("kev", RepositoryFiles.ReadText("feeders/packages/usgs.yaml"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("E-SCHEMA-PACKAGE", (await ApiServer.JsonOf(response)).GetProperty("code").GetString());
    }

    /// <summary>A write without the key is 401 E-AUTH.</summary>
    [Fact]
    public async Task A_write_without_the_key_is_unauthorized()
    {
        StringContent content = new StringContent(RepositoryFiles.ReadText("feeders/packages/usgs.yaml"), Encoding.UTF8, "application/yaml");
        HttpResponseMessage response = await _server.Client.PutAsync("/schemas/usgs", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("E-AUTH", (await ApiServer.JsonOf(response)).GetProperty("code").GetString());
    }
}
