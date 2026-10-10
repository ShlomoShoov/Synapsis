using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Synapsis.Storage.Dal;

/// <summary>
/// Talks to Elasticsearch through its REST API: send a JSON request, get the JSON answer. Kept this thin on purpose:
/// the queries themselves live in the repositories, written as the JSON Elasticsearch documents show.
/// </summary>
public interface IElasticClient
{
    /// <summary>False when no Elasticsearch address is set: callers then skip Elasticsearch completely.</summary>
    bool Enabled { get; }

    /// <summary>True when the path exists (HEAD request), such as an index name.</summary>
    Task<bool> ExistsAsync(string path);

    /// <summary>Sends a request with an optional JSON body and returns the JSON answer; an error answer throws <see cref="HttpRequestException"/>.</summary>
    Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body);
}
