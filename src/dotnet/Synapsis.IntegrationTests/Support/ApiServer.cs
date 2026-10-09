using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Synapsis.IntegrationTests.Support;

/// <summary>
/// The real server, started in-process on a fresh database, with the API key "test-key". Each test gets its own, so
/// tests never see each other's data. Requests go through the whole HTTP pipeline.
/// </summary>
public sealed class ApiServer : IDisposable
{
    /// <summary>The key writes carry.</summary>
    public const string Key = "test-key";

    private readonly TestDatabase _database = new TestDatabase();
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Starts the server; start-up seeds core before the first request.</summary>
    public ApiServer()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("Mongo:Url", _database.Options.Url);
            host.UseSetting("Mongo:Database", _database.Options.Database);
            host.UseSetting("Api:Key", Key);
        });
        Client = _factory.CreateClient();
    }

    /// <summary>A client of the server.</summary>
    public HttpClient Client { get; }

    /// <summary>The database the server uses.</summary>
    public string DatabaseName
    {
        get { return _database.Options.Database; }
    }

    /// <summary>PUT /schemas/{name} with a package's text and the key.</summary>
    public Task<HttpResponseMessage> PutSchemaAsync(string name, string text)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, "/schemas/" + name);
        request.Content = new StringContent(text, Encoding.UTF8, "application/yaml");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        return Client.SendAsync(request);
    }

    /// <summary>POST /ingest with a gzip body and the key, as the feeders send it.</summary>
    public Task<HttpResponseMessage> IngestAsync(string body, bool dryRun)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, dryRun ? "/ingest?dry_run=true" : "/ingest");
        ByteArrayContent content = new ByteArrayContent(Gzip(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        return Client.SendAsync(request);
    }

    /// <summary>The JSON body of an answer.</summary>
    public static async Task<JsonElement> JsonOf(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Stops the server and deletes its database.</summary>
    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        _database.Dispose();
    }

    /// <summary>The text, gzip-compressed.</summary>
    private static byte[] Gzip(string text)
    {
        using MemoryStream output = new MemoryStream();
        using (GZipStream gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            gzip.Write(bytes, 0, bytes.Length);
        }
        return output.ToArray();
    }
}
