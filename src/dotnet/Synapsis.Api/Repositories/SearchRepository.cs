using Elastic.Clients.Elasticsearch;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Elasticsearch for the Analyze routes: the map and anything that is search or counting, on the records index
/// (one document per state of an entity, event or conclusion). Every query builds its conditions with SearchFilters.
/// The queries themselves are the next task.
/// </summary>
public sealed class SearchRepository
{
    private readonly ElasticsearchClient? _client;
    private readonly string _recordsIndex;

    /// <summary>
    /// Creates the repository over the Elasticsearch client (null when no address is set: the queries will then say
    /// search is not available) and the name of the records index ("synapsis-records").
    /// </summary>
    public SearchRepository(ElasticContext elasticContext, ElasticOptions elasticOptions)
    {
        _client = elasticContext.Client;
        _recordsIndex = elasticOptions.RecordsIndex;
    }
}
