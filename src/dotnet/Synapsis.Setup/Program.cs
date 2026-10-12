using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;

namespace Synapsis.Setup;

/// <summary>
/// Starts the setup server: it prepares every store once, then ends. Docker Compose starts the other services only after
/// it ends successfully; when a step fails, the error ends the process with a non-zero code and nothing else starts.
/// </summary>
public static class Program
{
    /// <summary>Builds the services, runs every step in order, then ends.</summary>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        AddSetup(builder);
        using IHost host = builder.Build();
        await host.Services.GetRequiredService<SetupOrchestrator>().RunAsync(CancellationToken.None);
    }

    /// <summary>Registers the store connections, the steps (in the order they run) and the orchestrator; the tests use it too.</summary>
    public static void AddSetup(HostApplicationBuilder builder)
    {
        builder.Services.AddSynapsisData(builder.Configuration);
        builder.Services.AddSynapsisNeo4j();
        builder.Services.AddSingleton<ISetupStep, CreateMongoIndexesStep>();
        builder.Services.AddSingleton<ISetupStep, CreateIngestLogIndexStep>();
        builder.Services.AddSingleton<ISetupStep, CreateRecordsIndexStep>();
        builder.Services.AddSingleton<ISetupStep, CreateNeo4jConstraintsStep>();
        builder.Services.AddSingleton<ISetupStep, CreateKafkaTopicsStep>();
        builder.Services.AddSingleton<SetupOrchestrator>();
    }
}
