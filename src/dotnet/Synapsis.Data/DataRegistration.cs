using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data;

/// <summary>
/// The functions a service calls to use Synapsis.Data: one reads the shared settings and registers the options and
/// the two main connections; the others add the Kafka and Neo4j connections for the services that need them.
/// </summary>
public static class DataRegistration
{
    /// <summary>The file with the settings every service shares (store addresses, index and topic names, the embedder).</summary>
    public const string SharedSettingsFile = "sharedsettings.json";

    /// <summary>
    /// Reads the shared settings file below every other source (a service's appsettings.json and the environment
    /// override it), then registers the settings of each store, the MongoDB, Elasticsearch and embedder connections
    /// and the MongoDB health check (all singletons: one connection per process).
    /// </summary>
    public static IServiceCollection AddSynapsisData(this IServiceCollection services, ConfigurationManager configuration)
    {
        // 1. The shared settings, first in the list, so every later source wins over them.
        JsonConfigurationSource sharedSettings = new JsonConfigurationSource { Path = Path.Combine(AppContext.BaseDirectory, SharedSettingsFile) };
        sharedSettings.ResolveFileProvider();
        configuration.Sources.Insert(0, sharedSettings);

        // 2. The settings of each store.
        services.AddSingleton(configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions());
        services.AddSingleton(configuration.GetSection("Elastic").Get<ElasticOptions>() ?? new ElasticOptions());
        services.AddSingleton(configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions());
        services.AddSingleton(configuration.GetSection("Neo4j").Get<Neo4jOptions>() ?? new Neo4jOptions());
        services.AddSingleton(configuration.GetSection("Embedder").Get<EmbedderOptions>() ?? new EmbedderOptions());

        // 3. The connections (each one is made only when a service asks for it).
        services.AddSingleton<MongoContext>();
        services.AddSingleton<ElasticContext>();
        services.AddSingleton<EmbedderClient>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo");
        return services;
    }

    /// <summary>Registers the reusable Kafka producer connection for a service (its settings come from AddSynapsisData).</summary>
    public static IServiceCollection AddSynapsisKafka(this IServiceCollection services)
    {
        services.AddSingleton<KafkaContext>();
        return services;
    }

    /// <summary>Registers the reusable manual-commit Kafka consumer connection for a service (its group id comes from the service's appsettings.json).</summary>
    public static IServiceCollection AddSynapsisKafkaConsumer(this IServiceCollection services)
    {
        services.AddSingleton<KafkaConsumerContext>();
        return services;
    }

    /// <summary>Registers one reusable Neo4j driver connection (its settings come from AddSynapsisData).</summary>
    public static IServiceCollection AddSynapsisNeo4j(this IServiceCollection services)
    {
        services.AddSingleton<Neo4jContext>();
        return services;
    }
}
