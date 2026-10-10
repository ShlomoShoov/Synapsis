using System.Text.Json.Serialization;

namespace Synapsis.Storage.Models;

/// <summary>How many records one error code refused, such as { "code": "E-UNIT", "count": 3 }.</summary>
public sealed class RejectionCount
{
    /// <summary>The error code, such as "E-UNIT".</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>How many records it refused.</summary>
    [JsonPropertyName("count")]
    public long Count { get; set; }
}
