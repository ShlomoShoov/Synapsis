using Microsoft.OpenApi.Models;
using Synapsis.Api.Services.Swagger;
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
using Synapsis.Storage;
using Synapsis.Storage.Models;
// The composition root: every component of the server is registered here, in three groups, then the HTTP pipeline.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApiOptions api = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
MongoOptions mongo = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
ElasticOptions elastic = builder.Configuration.GetSection("Elastic").Get<ElasticOptions>() ?? new ElasticOptions();

builder.Services.AddSynapsisSil();
builder.Services.AddSynapsisStorage(mongo, elastic);
AddProcesses(builder.Services);
AddHttp(builder, api);

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.UseRequestDecompression();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

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

// HTTP: controllers with snake_case JSON, the key filter, gzip bodies, the body limit, browser reads from the UI (CORS),
// errors as {code, message, details}.
static void AddHttp(WebApplicationBuilder builder, ApiOptions api)
{
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = api.MaxBodyMb * 1024L * 1024L);
    builder.Services.AddSingleton(api);
    builder.Services.AddSingleton<ApiKeyFilter>();
    builder.Services.AddControllers()
        .AddJsonOptions(json => ApiJson.Apply(json.JsonSerializerOptions))
        .ConfigureApiBehaviorOptions(behavior => behavior.InvalidModelStateResponseFactory = ErrorMapper.ToParameterResult);
    builder.Services.AddRequestDecompression();
    AddSwagger(builder.Services);
    builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.WithOrigins(api.CorsOrigins).AllowAnyHeader().WithMethods("GET")));
    builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
    builder.Services.AddProblemDetails();
}

// Swagger: the API described at /swagger/v1/swagger.json and testable in the browser at /swagger, in every environment.
// The key box ("Authorize") sends X-Api-Key for the writes.
static void AddSwagger(IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(swagger =>
    {
        swagger.SwaggerDoc("v1", new OpenApiInfo { Title = "Synapsis API", Version = "v1" });
        swagger.OperationFilter<RawBodyOperationFilter>();
        OpenApiSecurityScheme key = new OpenApiSecurityScheme { Name = "X-Api-Key", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey };
        swagger.AddSecurityDefinition("ApiKey", key);
        OpenApiSecurityScheme keyReference = new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } };
        swagger.AddSecurityRequirement(new OpenApiSecurityRequirement { [keyReference] = new string[0] });
    });
}

/// <summary>The program's entry point, named so the HTTP tests can start the server in-process.</summary>
public partial class Program
{
}
