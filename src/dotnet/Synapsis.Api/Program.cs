using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synapsis.Api.Models.Configuration;
using Synapsis.Api.Services.Http;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Api.Orchestrators;
using Synapsis.Api.Services.Ingestion;
using Synapsis.Api.Services.Startup;
using Synapsis.Api.Services.Vocabulary;
using Synapsis.Sil;
using Synapsis.Sil.Services.Fingerprinting;
using Synapsis.Sil.Orchestrators;
using Synapsis.Sil.Services.Reading;
using Synapsis.Sil.Services.Registry;
using Synapsis.Sil.Services.Rules.Batch;
using Synapsis.Sil.Services.Versioning;
using Synapsis.Storage;
using Synapsis.Storage.Models;
// The composition root: every component of the server is registered here, in three groups, then the HTTP pipeline.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApiOptions api = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
MongoOptions mongo = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();

AddRecordLanguage(builder.Services);
builder.Services.AddSynapsisStorage(mongo);
AddProcesses(builder.Services);
AddHttp(builder, api);

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.UseRequestDecompression();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// Synapsis.Sil: the record language, built by its own composition (SilComponents), which holds the rule order.
static void AddRecordLanguage(IServiceCollection services)
{
    services.AddSingleton<IBundleReader>(SilComponents.CreateBundleReader());
    services.AddSingleton<IPackageReader>(SilComponents.CreatePackageReader());
    services.AddSingleton<IFingerprinter>(SilComponents.CreateFingerprinter());
    services.AddSingleton<IRegistryBuilder>(SilComponents.CreateRegistryBuilder());
    services.AddSingleton<IPackageValidator>(SilComponents.CreatePackageValidator());
    services.AddSingleton<IVersionPlanner>(SilComponents.CreateVersionPlanner());
    services.AddSingleton<IBatchValidator>(provider => SilComponents.CreateBatchValidator(provider.GetRequiredService<IReferenceResolver>()));
}

// The processes: the vocabulary in force, registration, ingestion, and the work done once at start-up.
static void AddProcesses(IServiceCollection services)
{
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IVocabularyProvider, VocabularyProvider>();
    services.AddSingleton<IRegistrationOrchestrator, RegistrationOrchestrator>();
    services.AddSingleton<IReceiptBuilder, ReceiptBuilder>();
    services.AddSingleton<IIngestionOrchestrator, IngestionOrchestrator>();
    services.AddHostedService<StartupInitializer>();
}

// HTTP: controllers with snake_case JSON, the key filter, gzip bodies, the body limit, errors as {code, message, details}.
static void AddHttp(WebApplicationBuilder builder, ApiOptions api)
{
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = api.MaxBodyMb * 1024L * 1024L);
    builder.Services.AddSingleton(api);
    builder.Services.AddSingleton<ApiKeyFilter>();
    builder.Services.AddControllers()
        .AddJsonOptions(json => ApiJson.Apply(json.JsonSerializerOptions))
        .ConfigureApiBehaviorOptions(behavior => behavior.InvalidModelStateResponseFactory = ErrorMapper.ToParameterResult);
    builder.Services.AddRequestDecompression();
    builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
    builder.Services.AddProblemDetails();
}

/// <summary>The program's entry point, named so the HTTP tests can start the server in-process.</summary>
public partial class Program
{
}
