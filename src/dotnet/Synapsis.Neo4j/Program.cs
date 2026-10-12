using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Neo4j;

/// <summary>Starts the Neo4j consumer service.</summary>
public static class Program
{
    /// <summary>Builds the host and starts the background orchestrator.</summary>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        MongoOptions mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
        builder.Services.AddSingleton(mongoOptions);
        builder.Services.AddSingleton<MongoContext>();
        builder.Services.AddSynapsisKafkaConsumer(builder.Configuration);
        builder.Services.AddSynapsisNeo4j(builder.Configuration);
        builder.Services.AddSingleton<MongoRecordRepository>();
        builder.Services.AddSingleton<Neo4jRepository>();
        builder.Services.AddHostedService<Neo4jOrchestrator>();

        IHost app = builder.Build();
        await app.RunAsync();
    }
}
