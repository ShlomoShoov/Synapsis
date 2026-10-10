using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Dal;

/// <summary>
/// The Elasticsearch connection: one <see cref="HttpClient"/> for the whole process (registered as a singleton), with a
/// short timeout so a slow Elasticsearch never holds up the API for long.
/// </summary>
public sealed class ElasticClient : IElasticClient
{
    /// <summary>The HTTP connection; null when no address is set.</summary>
    private readonly HttpClient? _http;

    /// <summary>Creates the client for the address in the options (none when it is empty).</summary>
    public ElasticClient(ElasticOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            _http = new HttpClient { BaseAddress = new Uri(options.Url), Timeout = TimeSpan.FromSeconds(5) };
        }
    }

    /// <inheritdoc/>
    public bool Enabled
    {
        get { return _http != null; }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string path)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);
        using HttpResponseMessage response = await Http().SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }
        using HttpResponseMessage response = await Http().SendAsync(request);
        string text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Elasticsearch answered {(int)response.StatusCode} to {method} {path}: {Shorten(text)}");
        }
        return JsonNode.Parse(text);
    }

    /// <summary>The connection; calling Elasticsearch while it is off is a bug in the caller.</summary>
    private HttpClient Http()
    {
        if (_http == null)
        {
            throw new InvalidOperationException("Elasticsearch is off (no Elastic:Url); check Enabled first.");
        }
        return _http;
    }

    /// <summary>The start of an error answer, enough to see what went wrong without flooding the log.</summary>
    private static string Shorten(string text)
    {
        if (text.Length <= 300)
        {
            return text;
        }
        return text.Substring(0, 300) + "…";
    }
}
