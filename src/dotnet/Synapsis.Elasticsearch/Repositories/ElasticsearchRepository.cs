using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Options;

namespace Synapsis.Elasticsearch;

/// <summary>
/// The records index in Elasticsearch, for the indexer: is a state already there, and write one. Each document's id is
/// the MongoDB document key, "{id}|{source}|{state}". When Elasticsearch does not answer, is too busy (429) or fails
/// (5xx), the methods throw, so the message is tried again; a document Elasticsearch refuses is answered with the reason.
/// </summary>
public sealed class ElasticsearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticOptions _elasticOptions;

    /// <summary>Creates the repository over the Elasticsearch connection; without an address the indexer cannot run.</summary>
    public ElasticsearchRepository(ElasticContext elasticContext, ElasticOptions elasticOptions)
    {
        _client = elasticContext.Client ?? throw new InvalidOperationException("Elastic:Url is empty: the indexer needs Elasticsearch.");
        _elasticOptions = elasticOptions;
    }

    /// <summary>True when the document with this key is already in the index.</summary>
    public async Task<bool> ExistsAsync(string documentKey, CancellationToken cancellationToken)
    {
        ExistsRequest request = new ExistsRequest(_elasticOptions.RecordsIndex, documentKey);
        ExistsResponse response = await _client.ExistsAsync(request, cancellationToken);
        if (response.Exists)
        {
            return true;
        }
        if (response.ApiCallDetails.HttpStatusCode == 404)
        {
            return false;
        }
        throw new InvalidOperationException("Elasticsearch did not say whether " + documentKey + " exists: " + response.DebugInformation);
    }

    /// <summary>Writes the document under its key: null when written, otherwise the reason Elasticsearch refused it.</summary>
    public async Task<string?> WriteAsync(string documentKey, SearchDocument document, CancellationToken cancellationToken)
    {
        IndexRequest<SearchDocument> request = new IndexRequest<SearchDocument>(document, _elasticOptions.RecordsIndex, documentKey);
        IndexResponse response = await _client.IndexAsync(request, cancellationToken);
        if (response.IsValidResponse)
        {
            return null;
        }
        int? status = response.ApiCallDetails.HttpStatusCode;
        if (status == null || status == 429 || status >= 500)
        {
            throw new InvalidOperationException("Elasticsearch is not available: " + response.DebugInformation);
        }
        if (response.ElasticsearchServerError?.Error?.Reason != null)
        {
            return response.ElasticsearchServerError.Error.Reason;
        }
        return response.DebugInformation;
    }
}
