using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Synapsis.Data.Models.Requests;

/// <summary>The body of the embedder's POST /embed: the texts to turn into vectors and the model's name.</summary>
public sealed class EmbedRequest
{
    /// <summary>The texts (the indexer sends one at a time).</summary>
    [JsonPropertyName("texts")]
    public List<string> Texts { get; set; } = new List<string>();

    /// <summary>The model's name in the embedder, such as "search".</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
}
