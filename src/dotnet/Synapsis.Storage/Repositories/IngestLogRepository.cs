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
/// Keeps the ingestion log in Elasticsearch with the official client. The index is created with an explicit mapping
/// the first time an entry is written, so every field has the type the UI's queries expect.
/// </summary>
public sealed class IngestLogRepository : IIngestLogRepository
{
    /// <summary>The index name.</summary>
    public const string IndexName = "synapsis-ingest-log";

    private readonly IElasticContext _elastic;
    private readonly ILogger<IngestLogRepository> _logger;

    /// <summary>True once the index is known to exist, so it is checked only once per process.</summary>
    private bool _indexReady;

    /// <summary>Creates the repository over the Elasticsearch connection; warnings go to the given logger.</summary>
    public IngestLogRepository(IElasticContext elastic, ILogger<IngestLogRepository> logger)
    {
        _elastic = elastic;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddAsync(IReadOnlyList<IngestLogEntry> entries)
    {
        ElasticsearchClient? client = _elastic.Client;
        if (client == null || entries.Count == 0)
        {
            return;
        }
        if (!await EnsureIndexAsync(client))
        {
            return;
        }
        BulkResponse response = await client.IndexManyAsync(entries, IndexName);
        if (!response.IsValidResponse)
        {
            _logger.LogWarning("The ingestion log could not be written (the records are stored): {Reason}", response.DebugInformation);
        }
    }

    /// <summary>Creates the index with its mapping when it does not exist yet; false (and a warning) when Elasticsearch fails.</summary>
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
    /// The fields of a log document and their types. Names and sources are keywords (exact values, for filters and
    /// totals); rejection_counts is "nested" so each code keeps its own count; the detailed rejections are stored but
    /// not searchable.
    /// </summary>
    private static TypeMapping Mapping()
    {
        Properties counts = new Properties();
        counts.Add("code", new KeywordProperty());
        counts.Add("count", new LongNumberProperty());
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
        fields.Add("rejection_counts", new NestedProperty { Properties = counts });
        fields.Add("rejections", new ObjectProperty { Enabled = false });
        return new TypeMapping { Properties = fields };
    }
}
