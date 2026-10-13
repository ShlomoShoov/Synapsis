using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synapsis.MockApi.Models.Options;
using Synapsis.MockApi.Repositories;
using Synapsis.MockApi.Services;

namespace Synapsis.MockApi;

/// <summary>
/// The mock's entry point. A named class in a namespace (not top-level statements), so the tests can start it beside
/// the real API, whose Program has no namespace.
/// </summary>
public sealed class Program
{
    /// <summary>Reads the settings, registers the components, reads the sample, then serves HTTP.</summary>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        MockOptions mockOptions = builder.Configuration.GetSection("Mock").Get<MockOptions>() ?? new MockOptions();

        // 1. Components: the sample, the logic of the routes, HTTP.
        builder.Services.AddSingleton<SampleRepository>();
        builder.Services.AddSingleton<AnalyzeService>();
        AddHttp(builder.Services, mockOptions);

        // 2. Read the sample now, not on the first request.
        WebApplication app = builder.Build();
        app.Services.GetRequiredService<SampleRepository>();

        // 3. The HTTP pipeline: errors as built-in problem answers, browser access (CORS), the routes.
        app.UseExceptionHandler();
        app.UseCors();
        app.MapControllers();
        app.Run();
    }

    /// <summary>Controllers with the real API's JSON (snake_case, null values left out), problem answers, CORS for the UI.</summary>
    private static void AddHttp(IServiceCollection services, MockOptions mockOptions)
    {
        services.AddControllers().AddJsonOptions(json =>
        {
            json.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            json.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        services.AddProblemDetails();
        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.WithOrigins(mockOptions.CorsOrigins).AllowAnyHeader().WithMethods("GET")));
    }
}
