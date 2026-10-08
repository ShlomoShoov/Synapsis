using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>Valid time. <c>Precision</c> is a value from the core <c>precision</c> vocabulary
/// (second, minute, hour, day, week, month, quarter, year).</summary>
public sealed record TimeSpec
{
    [JsonPropertyName("start")]
    public required DateTimeOffset Start { get; init; }

    [JsonPropertyName("end")]
    public DateTimeOffset? End { get; init; }

    [JsonPropertyName("precision")]
    public required string Precision { get; init; }
}

/// <summary>At least one form must be present, and the forms allowed depend on the type.</summary>
public sealed record Place
{
    /// <summary><c>[lon, lat]</c>; requires <see cref="UncertaintyKm"/>.</summary>
    [JsonPropertyName("point")]
    public IReadOnlyList<double>? Point { get; init; }

    [JsonPropertyName("uncertainty_km")]
    public double? UncertaintyKm { get; init; }

    [JsonPropertyName("admin")]
    public AdminPlace? Admin { get; init; }

    [JsonPropertyName("logical")]
    public LogicalPlace? Logical { get; init; }

    [JsonPropertyName("unplaced")]
    public bool? Unplaced { get; init; }
}

public sealed record AdminPlace
{
    /// <summary>ISO 3166-1 alpha-2.</summary>
    [JsonPropertyName("country")]
    public required string Country { get; init; }

    /// <summary>ISO 3166-2, e.g. <c>US-CA</c>.</summary>
    [JsonPropertyName("subdivision")]
    public string? Subdivision { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>A path in a logical space registered by a package, e.g. <c>cyber.vendor_product</c>.</summary>
public sealed record LogicalPlace
{
    [JsonPropertyName("space")]
    public required string Space { get; init; }

    [JsonPropertyName("path")]
    public required IReadOnlyList<string> Path { get; init; }
}

public sealed record Participant
{
    /// <summary>An entity id.</summary>
    [JsonPropertyName("entity")]
    public required string Entity { get; init; }

    /// <summary>A role the event type declares.</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }
}

public sealed record Measure
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("value")]
    public required double Value { get; init; }

    /// <summary>Must equal the unit the measure is registered with.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}

/// <summary>The words semantic search embeds.</summary>
public sealed record TextBlock
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("lang")]
    public string? Lang { get; init; }
}

/// <summary>Where in the source the record came from.</summary>
public sealed record Evidence
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("row")]
    public string? Row { get; init; }

    /// <summary><c>[start, end]</c> character offsets.</summary>
    [JsonPropertyName("span")]
    public IReadOnlyList<int>? Span { get; init; }
}

/// <summary>What a conclusion claims: a predicate with an object or a value.</summary>
public sealed record Statement
{
    [JsonPropertyName("predicate")]
    public required string Predicate { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("value")]
    public JsonNode? Value { get; init; }
}

/// <summary>Who or what produced a conclusion.</summary>
public sealed record Method
{
    /// <summary><c>human</c>, <c>agent</c> or <c>system</c>.</summary>
    [JsonPropertyName("by")]
    public required string By { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}
