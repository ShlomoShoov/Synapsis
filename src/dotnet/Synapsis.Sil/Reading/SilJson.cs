using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Sil.Reading;

/// <summary>
/// The JSON settings for SIL records and bundles, shared by every reader and writer of them.
/// </summary>
public static class SilJson
{
    /// <summary>
    /// The settings, built once:
    /// C# property names map to snake_case JSON names (KnownAtBasis to "known_at_basis");
    /// a JSON field the C# classes do not know is an error, never silently dropped;
    /// enums are written in snake_case ("entity", "event");
    /// absent values are left out when writing, never written as null.
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Builds the settings described on <see cref="Options"/>.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
