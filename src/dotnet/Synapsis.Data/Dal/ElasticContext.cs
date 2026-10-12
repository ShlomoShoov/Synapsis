using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>
/// The official Elasticsearch client, made once per process. A short timeout keeps a slow Elasticsearch from holding
/// up ingestion. No address in the settings: no client, and the ingestion log is skipped.
/// </summary>
public sealed class ElasticContext
{
    private readonly ElasticOptions _options;

    /// <summary>Creates the client for the address in the options, or none when the address is empty.</summary>
    public ElasticContext(ElasticOptions options)
    {
        _options = options;
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            return;
        }
        ElasticsearchClientSettings settings = new ElasticsearchClientSettings(new Uri(options.Url));
        settings.RequestTimeout(TimeSpan.FromSeconds(5));
        Client = new ElasticsearchClient(settings);
    }

    /// <summary>The client, or null when no address is set.</summary>
    public ElasticsearchClient? Client { get; }

    /// <summary>
    /// Creates the ingestion log index with its field types when it does not exist (nothing to do without a client).
    /// Throws when Elasticsearch fails, so a start-up without the log is noticed.
    /// </summary>
    public async Task CreateIngestLogIndexAsync()
    {
        if (Client == null)
        {
            return;
        }
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse exists = await Client.Indices.ExistsAsync(_options.IngestLogIndex);
        if (exists.Exists)
        {
            return;
        }
        CreateIndexResponse created = await Client.Indices.CreateAsync(new CreateIndexRequest(_options.IngestLogIndex) { Mappings = IngestLogMapping() });
        if (!created.IsValidResponse)
        {
            throw new InvalidOperationException("The ingestion log index could not be created: " + created.DebugInformation);
        }
    }

    /// <summary>
    /// The field types of a log document. Names and sources are keywords (exact values, for filters and totals);
    /// rejection_counts is "nested" so each code keeps its own count; the listed rejections are stored, not searchable.
    /// </summary>
    private static TypeMapping IngestLogMapping()
    {
        Properties codeCount = new Properties();
        codeCount.Add("code", new KeywordProperty());
        codeCount.Add("count", new LongNumberProperty());
        Properties fields = new Properties();
        fields.Add("timestamp", new DateProperty());
        fields.Add("batch_id", new KeywordProperty());
        fields.Add("producer", new KeywordProperty());
        fields.Add("source", new KeywordProperty());
        fields.Add("received", new LongNumberProperty());
        fields.Add("new", new LongNumberProperty());
        fields.Add("changed", new LongNumberProperty());
        fields.Add("unchanged", new LongNumberProperty());
        fields.Add("duplicates", new LongNumberProperty());
        fields.Add("rejected", new LongNumberProperty());
        fields.Add("duration_ms", new LongNumberProperty());
        fields.Add("rejection_counts", new NestedProperty { Properties = codeCount });
        fields.Add("rejections", new ObjectProperty { Enabled = false });
        return new TypeMapping { Properties = fields };
    }
}
