namespace Synapsis.Data.Models.Options;

/// <summary>
/// Where Elasticsearch is and the names of its indexes, from the configuration section "Elastic" (sharedsettings.json;
/// environment variables such as Elastic__Url override it).
/// </summary>
public sealed class ElasticOptions
{
    /// <summary>The address, such as "http://elasticsearch:9200". Empty: the API writes no ingestion log.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The index of the ingestion log (one document per batch and source; the UI reads it).</summary>
    public string IngestLogIndex { get; set; } = string.Empty;

    /// <summary>The index of the search documents (one document per state of an entity, event or conclusion).</summary>
    public string RecordsIndex { get; set; } = string.Empty;
}
