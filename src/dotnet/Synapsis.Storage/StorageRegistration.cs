using Microsoft.Extensions.DependencyInjection;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories;

namespace Synapsis.Storage;

/// <summary>
/// Registers Synapsis.Storage for dependency injection: the two connections, the three repositories and the MongoDB
/// health check. Each is a singleton (one connection per process).
/// </summary>
public static class StorageRegistration
{
    /// <summary>Adds the storage components, connected to the MongoDB and Elasticsearch the options name.</summary>
    public static IServiceCollection AddSynapsisStorage(this IServiceCollection services, MongoOptions mongoOptions, ElasticOptions elasticOptions)
    {
        services.AddSingleton(mongoOptions);
        services.AddSingleton(elasticOptions);
        services.AddSingleton<MongoContext>();
        services.AddSingleton<ElasticContext>();
        services.AddSingleton<RecordRepository>();
        services.AddSingleton<CatalogRepository>();
        services.AddSingleton<IngestLogRepository>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo");
        return services;
    }
}
