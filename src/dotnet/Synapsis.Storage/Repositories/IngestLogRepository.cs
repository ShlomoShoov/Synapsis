using System.Collections.Generic;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// The ingestion log in Elasticsearch (index "synapsis-ingest-log"), written with the official client. Best effort:
/// when Elasticsearch fails, a warning is logged and ingestion goes on (the records are already stored).
/// </summary>
public sealed class IngestLogRepository
{
    /// <summary>The index name.</summary>
    public const string IndexName = "synapsis-ingest-log";

    private readonly ElasticContext _elasticContext;
    private readonly ILogger<IngestLogRepository> _logger;

    /// <summary>True once the index is known to exist, so it is checked only once per process.</summary>
    private bool _indexReady;

    /// <summary>Creates the repository over the Elasticsearch connection; warnings go to the given logger.</summary>
    public IngestLogRepository(ElasticContext elasticContext, ILogger<IngestLogRepository> logger)
    {
        _elasticContext = elasticContext;
        _logger = logger;
    }

    /// <summary>Writes the summaries, one document each. No Elasticsearch address: nothing is written.</summary>
    public async Task AddAsync(IReadOnlyList<IngestSummary> summaries)
    {
        ElasticsearchClient? client = _elasticContext.Client;
        if (client == null || summaries.Count == 0)
        {
            return;
        }
        if (!await EnsureIndexAsync(client))
        {
            return;
        }
        BulkResponse response = await client.IndexManyAsync(summaries, IndexName);
        if (!response.IsValidResponse)
        {
            _logger.LogWarning("The ingestion log could not be written (the records are stored): {Reason}", response.DebugInformation);
        }
    }

    /// <summary>Creates the index with its field types when it does not exist yet; false (and a warning) when Elasticsearch fails.</summary>
    private async Task<bool> EnsureIndexAsync(ElasticsearchClient client)
    {
        if (_indexReady)
        {
            return true;
        }
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse exists = await client.Indices.ExistsAsync(IndexName);
        if (!exists.Exists)
        {
            CreateIndexResponse created = await client.Indices.CreateAsync(new CreateIndexRequest(IndexName) { Mappings = Mapping() });
            if (!created.IsValidResponse)
            {
                _logger.LogWarning("The ingestion log index could not be created: {Reason}", created.DebugInformation);
                return false;
            }
        }
        _indexReady = true;
        return true;
    }

    /// <summary>
    /// The field types of a log document. Names and sources are keywords (exact values, for filters and totals);
    /// rejection_counts is "nested" so each code keeps its own count; the listed rejections are stored, not searchable.
    /// </summary>
    private static TypeMapping Mapping()
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
