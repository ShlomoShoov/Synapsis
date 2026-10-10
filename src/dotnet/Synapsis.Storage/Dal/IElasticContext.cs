using Elastic.Clients.Elasticsearch;

namespace Synapsis.Storage.Dal;

/// <summary>The connection to Elasticsearch, made once per process (like <see cref="IMongoContext"/> for MongoDB).</summary>
public interface IElasticContext
{
    /// <summary>The official Elasticsearch client; null when no address is set (Elastic:Url empty), which turns the ingestion log off.</summary>
    ElasticsearchClient? Client { get; }
}
