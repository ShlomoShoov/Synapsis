using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Synapsis.Api.Models;
using Synapsis.Api.Orchestrators;
using Synapsis.Storage;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;

// The composition root: read the settings, register the components, create the MongoDB index, start HTTP.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApiOptions apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
MongoOptions mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
ElasticOptions elasticOptions = builder.Configuration.GetSection("Elastic").Get<ElasticOptions>() ?? new ElasticOptions();

// 1. Components: storage (MongoDB, Elasticsearch), the ingestion process, HTTP.
builder.Services.AddSynapsisStorage(mongoOptions, elasticOptions);
builder.Services.AddSingleton<IngestionOrchestrator>();
AddHttp(builder, apiOptions);

// 2. Start-up work, then the HTTP pipeline: errors as built-in problem answers, gzip bodies, browser access, Swagger.
WebApplication app = builder.Build();
await app.Services.GetRequiredService<MongoContext>().CreateIndexesAsync();
app.UseExceptionHandler();
app.UseRequestDecompression();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// HTTP: controllers with snake_case JSON, gzip bodies, the body limit, browser reads from the UI (CORS), Swagger.
static void AddHttp(WebApplicationBuilder builder, ApiOptions apiOptions)
{
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = apiOptions.MaxBodyMb * 1024L * 1024L);
    builder.Services.AddControllers().AddJsonOptions(json =>
    {
        json.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        json.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
    builder.Services.AddRequestDecompression();
    builder.Services.AddProblemDetails();
    builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.WithOrigins(apiOptions.CorsOrigins).AllowAnyHeader().WithMethods("GET")));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(swagger => swagger.SwaggerDoc("v1", new OpenApiInfo { Title = "Synapsis API", Version = "v1" }));
}

/// <summary>The program's entry point, named so the HTTP tests can start the server in-process.</summary>
public partial class Program
{
}
