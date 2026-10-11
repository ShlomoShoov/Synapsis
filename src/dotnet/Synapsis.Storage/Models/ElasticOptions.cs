namespace Synapsis.Storage.Models;

/// <summary>Where Elasticsearch is, from the configuration section "Elastic" (environment variable Elastic__Url).</summary>
public sealed class ElasticOptions
{
    /// <summary>The address, such as "http://elasticsearch:9200". Empty: the ingestion log is not written.</summary>
    public string Url { get; set; } = string.Empty;
}
