using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Results;

/// <summary>The embedder's answer to POST /embed: one vector per text, in the same order.</summary>
public sealed class EmbedAnswer
{
    /// <summary>The vectors (all zeros for a text with no known word).</summary>
    [JsonPropertyName("vectors")]
    public List<float[]> Vectors { get; set; } = new List<float[]>();
}
