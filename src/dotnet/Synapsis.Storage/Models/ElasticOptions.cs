namespace Synapsis.Storage.Models;

/// <summary>Where Elasticsearch runs (settings section "Elastic"). An empty URL turns the ingestion log off.</summary>
public sealed class ElasticOptions
{
    /// <summary>The Elasticsearch address, such as "http://elasticsearch:9200"; empty means no log.</summary>
    public string Url { get; init; } = string.Empty;
}
