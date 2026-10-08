using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>Something that happened at a time and a place, with participants and measures.</summary>
public sealed record EventRecord : SilRecord
{
    [JsonPropertyName("time")]
    public required TimeSpec Time { get; init; }

    [JsonPropertyName("place")]
    public required Place Place { get; init; }

    [JsonPropertyName("participants")]
    public IReadOnlyList<Participant>? Participants { get; init; }

    [JsonPropertyName("measures")]
    public IReadOnlyList<Measure>? Measures { get; init; }
}
