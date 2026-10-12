using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Setup;

/// <summary>Creates the index of the ingestion log in Elasticsearch (one document per batch and source; the UI reads it).</summary>
public sealed class CreateIngestLogIndexStep : ISetupStep
{
    private readonly ElasticContext _elasticContext;
    private readonly ElasticOptions _elasticOptions;

    /// <summary>Creates the step over the Elasticsearch connection and its index names.</summary>
    public CreateIngestLogIndexStep(ElasticContext elasticContext, ElasticOptions elasticOptions)
    {
        _elasticContext = elasticContext;
        _elasticOptions = elasticOptions;
    }

    /// <summary>Creates the index with its field types when it does not exist; throws when Elasticsearch refuses.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ElasticsearchClient client = _elasticContext.Client ?? throw new InvalidOperationException("Elastic:Url is empty: setup needs Elasticsearch.");

        // 1. Nothing to do when the index exists.
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse exists = await client.Indices.ExistsAsync(_elasticOptions.IngestLogIndex, cancellationToken);
        if (exists.Exists)
        {
            return;
        }

        // 2. Create it with its field types.
        CreateIndexRequest request = new CreateIndexRequest(_elasticOptions.IngestLogIndex) { Mappings = IngestLogMapping() };
        CreateIndexResponse created = await client.Indices.CreateAsync(request, cancellationToken);
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
