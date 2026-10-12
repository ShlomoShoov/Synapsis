using System.Text.Json;
using Synapsis.Data.Models.Records;

namespace Synapsis.Api.Models.Results;

/// <summary>A record that passed validation: read into its class, and kept as the JSON that arrived (which is stored).</summary>
public sealed class ValidRecord
{
    /// <summary>The record as it arrived.</summary>
    public JsonElement Json { get; set; }

    /// <summary>The record read into the class of its kind.</summary>
    public SilRecord Record { get; set; } = null!;
}
