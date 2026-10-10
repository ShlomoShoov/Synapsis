using Microsoft.Extensions.DependencyInjection;
using Synapsis.Sil.Services.Rules.Batch;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories;
namespace Synapsis.Storage;

/// <summary>
/// Registers Synapsis.Storage for dependency injection: the connection, the initializer, the repositories (records and
/// packages in MongoDB, the ingestion log in Elasticsearch) and the MongoDB health check. Everything is a singleton (one connection per process). The package repository needs
/// Synapsis.Sil's IPackageReader, which the caller registers.
/// </summary>
public static class StorageRegistration
{
    /// <summary>Adds the storage components, connected to the MongoDB database and the Elasticsearch the options name.</summary>
    public static IServiceCollection AddSynapsisStorage(this IServiceCollection services, MongoOptions options, ElasticOptions elastic)
    {
        services.AddSingleton(options);
        services.AddSingleton(elastic);
        services.AddSingleton<IElasticClient, ElasticClient>();
        services.AddSingleton<IIngestLogRepository, IngestLogRepository>();
        services.AddSingleton<IMongoContext, MongoContext>();
        services.AddSingleton<IStorageInitializer, StorageInitializer>();
        services.AddSingleton<IRecordRepository, RecordRepository>();
        services.AddSingleton<IReferenceResolver>(provider => provider.GetRequiredService<IRecordRepository>());
        services.AddSingleton<IPackageRepository, PackageRepository>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo");
        return services;
    }
}
