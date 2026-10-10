using System;
using Elastic.Clients.Elasticsearch;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Dal;

/// <summary>
/// Creates the official Elasticsearch client from the settings. A short request timeout keeps a slow Elasticsearch
/// from holding up ingestion for long.
/// </summary>
public sealed class ElasticContext : IElasticContext
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

    /// <inheritdoc/>
    public ElasticsearchClient? Client { get; }
}
