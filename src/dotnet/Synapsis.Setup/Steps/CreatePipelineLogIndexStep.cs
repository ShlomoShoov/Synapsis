using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Setup;

/// <summary>
/// Creates the index of the pipeline log in Elasticsearch: the relay, the indexer, the projector and the engine write
/// a few counters there (contracts/pipeline-log.md); the UI's Observe reads it.
/// </summary>
public sealed class CreatePipelineLogIndexStep : ISetupStep
{
    private readonly ElasticContext _elasticContext;
    private readonly ElasticOptions _elasticOptions;

    /// <summary>Creates the step over the Elasticsearch connection and its index names.</summary>
    public CreatePipelineLogIndexStep(ElasticContext elasticContext, ElasticOptions elasticOptions)
    {
        _elasticContext = elasticContext;
        _elasticOptions = elasticOptions;
    }

    /// <summary>Creates the index with its field types when it does not exist; throws when Elasticsearch refuses.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ElasticsearchClient client = _elasticContext.Client ?? throw new InvalidOperationException("Elastic:Url is empty: setup needs Elasticsearch.");

        // 1. Nothing to do when the index exists.
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse exists = await client.Indices.ExistsAsync(_elasticOptions.PipelineLogIndex, cancellationToken);
        if (exists.Exists)
        {
            return;
        }

        // 2. Create it with its field types.
        CreateIndexRequest request = new CreateIndexRequest(_elasticOptions.PipelineLogIndex) { Mappings = PipelineLogMapping() };
        CreateIndexResponse created = await client.Indices.CreateAsync(request, cancellationToken);
        if (!created.IsValidResponse)
        {
            throw new InvalidOperationException("The pipeline log index could not be created: " + created.DebugInformation);
        }
    }

    /// <summary>The field types of a pipeline log document, exactly as contracts/pipeline-log.md lists them.</summary>
    private static TypeMapping PipelineLogMapping()
    {
        Properties fields = new Properties();
        fields.Add("at", new DateProperty());
        fields.Add("service", new KeywordProperty());
        fields.Add("event", new KeywordProperty());
        fields.Add("processed", new LongNumberProperty());
        fields.Add("written", new LongNumberProperty());
        fields.Add("skipped", new LongNumberProperty());
        fields.Add("failed", new LongNumberProperty());
        fields.Add("lag", new LongNumberProperty());
        fields.Add("source", new KeywordProperty());
        fields.Add("message", new TextProperty());
        return new TypeMapping { Properties = fields };
    }
}
