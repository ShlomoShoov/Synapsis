using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>A thing that exists over time: a vendor, a bank, a vessel, an armed actor.</summary>
public sealed record EntityRecord : SilRecord
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("aliases")]
    public IReadOnlyList<string>? Aliases { get; init; }

    [JsonPropertyName("place")]
    public Place? Place { get; init; }
}
