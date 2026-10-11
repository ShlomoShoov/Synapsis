using System;
using Elastic.Clients.Elasticsearch;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Dal;

/// <summary>
/// The official Elasticsearch client, made once per process. A short timeout keeps a slow Elasticsearch from holding
/// up ingestion. No address in the settings: no client, and the ingestion log is skipped.
/// </summary>
public sealed class ElasticContext
{
    /// <summary>Creates the client for the address in the options, or none when the address is empty.</summary>
    public ElasticContext(ElasticOptions options)
    {
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
}
