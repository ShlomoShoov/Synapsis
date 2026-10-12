using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Results;

/// <summary>How many records one rejection code refused.</summary>
public sealed class CodeCount
{
    /// <summary>The code, such as "E-MISSING".</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>How many records it refused.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
