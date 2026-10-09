using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Synapsis.Sil.Models.Domain.Vocabulary.Attributes;

/// <summary>
/// One attribute a record type declares: its value type and the limits its values must respect.
/// Example (KEV): known_ransomware_campaign_use: {type: string, enum: [Known, Unknown]}.
/// </summary>
public sealed class AttributeDefinition
{
    /// <summary>The value type.</summary>
    public AttributeType Type { get; init; }

    /// <summary>Optional human title.</summary>
    public string? Title { get; init; }

    /// <summary>True when every record of the type must carry this attribute.</summary>
    public bool Required { get; init; }

    /// <summary>The longest text allowed, in characters.</summary>
    public int? MaxLength { get; init; }

    /// <summary>The most items a list may hold.</summary>
    public int? MaxItems { get; init; }

    /// <summary>The only values allowed (the "enum" key in YAML).</summary>
    [YamlMember(Alias = "enum")]
    public List<string>? AllowedValues { get; init; }

    /// <summary>A regular expression every text value must match.</summary>
    public string? Pattern { get; init; }

    /// <summary>The smallest number allowed.</summary>
    public double? Min { get; init; }

    /// <summary>The largest number allowed.</summary>
    public double? Max { get; init; }
}
