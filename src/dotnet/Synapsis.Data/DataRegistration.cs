using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data;

/// <summary>
/// The two functions a service calls to use Synapsis.Data: one registers it, one prepares the stores at start-up.
/// </summary>
public static class DataRegistration
{
    /// <summary>
    /// Reads the settings sections "Mongo" and "Elastic" and registers the two connections, the settings and the
    /// MongoDB health check (all singletons: one connection per process).
    /// </summary>
    public static IServiceCollection AddSynapsisData(this IServiceCollection services, IConfiguration configuration)
    {
        MongoOptions mongoOptions = configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
        ElasticOptions elasticOptions = configuration.GetSection("Elastic").Get<ElasticOptions>() ?? new ElasticOptions();
        services.AddSingleton(mongoOptions);
        services.AddSingleton(elasticOptions);
        services.AddSingleton<MongoContext>();
        services.AddSingleton<ElasticContext>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo");
        return services;
    }

    /// <summary>Registers the reusable Kafka producer connection for a service.</summary>
    public static IServiceCollection AddSynapsisKafka(this IServiceCollection services, IConfiguration configuration)
    {
        KafkaOptions kafkaOptions = configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
        services.AddSingleton(kafkaOptions);
        services.AddSingleton<KafkaContext>();
        return services;
    }

    /// <summary>Registers the reusable manual-commit Kafka consumer connection for a service.</summary>
    public static IServiceCollection AddSynapsisKafkaConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        KafkaOptions kafkaOptions = configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
        services.AddSingleton(kafkaOptions);
        services.AddSingleton<KafkaConsumerContext>();
        return services;
    }

    /// <summary>Registers the Neo4j settings and one reusable Neo4j driver connection.</summary>
    public static IServiceCollection AddSynapsisNeo4j(this IServiceCollection services, IConfiguration configuration)
    {
        Neo4jOptions neo4jOptions = configuration.GetSection("Neo4j").Get<Neo4jOptions>() ?? new Neo4jOptions();
        services.AddSingleton(neo4jOptions);
        services.AddSingleton<Neo4jContext>();
        return services;
    }

    /// <summary>
    /// Called once at start-up: creates what the stores need when it is missing (the MongoDB index, the ingestion log
    /// index in Elasticsearch). When everything exists, it does nothing.
    /// </summary>
    public static async Task InitializeSynapsisDataAsync(this IServiceProvider services)
    {
        await services.GetRequiredService<MongoContext>().CreateIndexesAsync();
        await services.GetRequiredService<ElasticContext>().CreateIngestLogIndexAsync();
    }
}
