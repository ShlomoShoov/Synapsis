using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synapsis.Data;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;
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

        // Bind configurations from existing sections
        MongoOptions mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
        // Register shared MongoDB and Kafka infrastructure
        builder.Services.AddSingleton(mongoOptions);
        builder.Services.AddSingleton<MongoContext>();
        builder.Services.AddSynapsisKafka(builder.Configuration);

        // Register Relay components
        builder.Services.AddSingleton<IDocumentChangeMonitor, MongoDocumentChangeMonitor>();
        builder.Services.AddHostedService<DocumentRelayOrchestrator>();

        IHost app = builder.Build();
        await app.RunAsync();
    }
}
