using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;

namespace Synapsis.Elasticsearch;

/// <summary>Starts the Elasticsearch consumer service.</summary>
public static class Program
{
    /// <summary>Builds the host and starts the background orchestrator.</summary>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSynapsisData(builder.Configuration);
        builder.Services.AddPipelineLog("indexer");
        builder.Services.AddSynapsisKafkaConsumer();
        builder.Services.AddSingleton<MongoRecordRepository>();
        builder.Services.AddSingleton<ElasticsearchRepository>();
        builder.Services.AddHostedService<ElasticsearchOrchestrator>();

        IHost app = builder.Build();
        await app.RunAsync();
    }
}
