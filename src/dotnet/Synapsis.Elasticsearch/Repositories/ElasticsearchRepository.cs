using System;
using System.Threading;
using System.Threading.Tasks;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Records;

namespace Synapsis.Elasticsearch;

/// <summary>Defines the Elasticsearch hand-off without adding indexing rules in this phase.</summary>
public sealed class ElasticsearchRepository
{
    private readonly ElasticContext _elasticContext;

    /// <summary>Creates the repository over the shared Elasticsearch client.</summary>
    public ElasticsearchRepository(ElasticContext elasticContext)
    {
        _elasticContext = elasticContext;
    }

    /// <summary>Accepts a typed record for the later indexing implementation.</summary>
    public Task StoreAsync(SilRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _ = _elasticContext;
        _ = cancellationToken;
        // TODO: Replace this placeholder with the actual Elasticsearch indexing call.
        throw new NotSupportedException("Elasticsearch record indexing is not implemented in this phase.");
    }
}
