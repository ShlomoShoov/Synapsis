using System.Text.Json;

namespace Synapsis.Api.Models.Records;

/// <summary>A conclusion: something a person, a model or the engine concluded about other records.</summary>
public sealed class ConclusionRecord : SilRecord
{
    /// <summary>What it is about: record ids or subjects (required, any JSON).</summary>
    public JsonElement? About { get; set; }

    /// <summary>What it says (required, any JSON).</summary>
    public JsonElement? Statement { get; set; }
}
