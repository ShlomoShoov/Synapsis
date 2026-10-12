using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;
using System.Threading.Tasks;

namespace Synapsis.KafkaRelay;

/// <summary>
/// Entry point for the Synapsis.KafkaRelay microservice.
/// </summary>
public static class Program
{
    /// <summary>
    /// Builds and runs the relay worker host.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // Register the shared settings, MongoDB and the Kafka producer
        builder.Services.AddSynapsisData(builder.Configuration);
        builder.Services.AddSynapsisKafka();

        // Register Relay components
        builder.Services.AddSingleton<IDocumentChangeMonitor, MongoDocumentChangeMonitor>();
        builder.Services.AddHostedService<DocumentRelayOrchestrator>();

        IHost app = builder.Build();
        await app.RunAsync();
    }
}
