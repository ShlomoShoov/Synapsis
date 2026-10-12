using System.Collections.Generic;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Results;

namespace Synapsis.Api.Repositories;

/// <summary>
/// The ingestion log in Elasticsearch (index "synapsis-ingest-log", created at start-up), written with the official
/// client. Best effort: when Elasticsearch fails, a warning is logged and ingestion goes on (the records are stored).
/// </summary>
public sealed class IngestLogRepository
{
    private readonly ElasticContext _elasticContext;
    private readonly ILogger<IngestLogRepository> _logger;

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
        BulkResponse response = await client.IndexManyAsync(summaries, ElasticContext.IngestLogIndex);
        if (!response.IsValidResponse)
        {
            _logger.LogWarning("The ingestion log could not be written (the records are stored): {Reason}", response.DebugInformation);
        }
    }
}
