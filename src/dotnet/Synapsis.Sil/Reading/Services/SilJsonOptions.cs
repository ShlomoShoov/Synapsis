using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Sil.Reading.Services;

/// <summary>
/// The JSON settings for SIL bundles and records, shared by the two JSON readers:
/// C# property names map to snake_case JSON names (KnownAtBasis reads "known_at_basis");
/// a JSON field the C# classes do not know is an error, never silently dropped;
/// enum values are snake_case text ("entity", "event").
/// </summary>
public static class SilJsonOptions
{
    /// <summary>The settings, built once.</summary>
    public static readonly JsonSerializerOptions Value = Create();

    /// <summary>Builds the settings.</summary>
    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
