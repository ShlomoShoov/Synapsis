using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Synapsis.Api.Models.Options;
using Synapsis.Api.Orchestrators;
using Synapsis.Api.Repositories;
using Synapsis.Data;

// The composition root: read the settings, register the components, start HTTP (the stores are prepared by Synapsis.Setup).
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApiOptions apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();

// 1. Components: the data (MongoDB, Elasticsearch, Neo4j), the repositories, the ingestion process, the read
//    routes, HTTP.
builder.Services.AddSynapsisData(builder.Configuration);
builder.Services.AddSynapsisNeo4j();
builder.Services.AddSingleton<RecordRepository>();
builder.Services.AddSingleton<CatalogRepository>();
builder.Services.AddSingleton<IngestLogRepository>();
builder.Services.AddSingleton<SearchRepository>();
builder.Services.AddSingleton<GraphRepository>();
builder.Services.AddSingleton<IngestionOrchestrator>();
AddHttp(builder, apiOptions);

// 2. The HTTP pipeline: errors as built-in problem answers, gzip bodies, browser access, Swagger.
WebApplication app = builder.Build();
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
