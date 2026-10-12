using System.Text.Json;

namespace Synapsis.Data.Models.Records;

/// <summary>What a conclusion says: a predicate with an object (a code) or a value (any JSON).</summary>
public sealed class Statement
{
    /// <summary>The predicate, such as "in_logical_space".</summary>
    public string? Predicate { get; set; }

    /// <summary>The object, when it is a code.</summary>
    public string? Object { get; set; }

    /// <summary>The value, when it is not a code.</summary>
    public JsonElement? Value { get; set; }
}
