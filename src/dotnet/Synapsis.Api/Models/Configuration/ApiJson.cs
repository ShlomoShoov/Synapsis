using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Api.Models.Configuration;

/// <summary>
/// The JSON settings of every answer: snake_case names like the contract (BatchId is "batch_id"), and fields without a
/// value left out. The controllers and the exception handler use the same settings.
/// </summary>
public static class ApiJson
{
    /// <summary>The settings, built once, for code that writes JSON itself.</summary>
    public static readonly JsonSerializerOptions Options = Apply(new JsonSerializerOptions());

    /// <summary>Applies the settings to a set of options (the framework's, for the controllers).</summary>
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        return options;
    }
}
