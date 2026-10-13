using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;

namespace Synapsis.Neo4j;

/// <summary>Starts the Neo4j consumer service.</summary>
public static class Program
{
    /// <summary>Builds the host and starts the background orchestrator.</summary>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSynapsisData(builder.Configuration);
        builder.Services.AddPipelineLog("projector");
        builder.Services.AddSynapsisKafkaConsumer();
        builder.Services.AddSynapsisNeo4j();
        builder.Services.AddSingleton<MongoRecordRepository>();
        builder.Services.AddSingleton<Neo4jRepository>();
        builder.Services.AddHostedService<Neo4jOrchestrator>();

        IHost app = builder.Build();
        await app.RunAsync();
    }
}
