namespace Synapsis.Data.Models.Records;

/// <summary>A record's text.</summary>
public sealed class TextBlock
{
    /// <summary>A short title.</summary>
    public string? Title { get; set; }

    /// <summary>The body.</summary>
    public string? Body { get; set; }

    /// <summary>The language code, such as "en".</summary>
    public string? Lang { get; set; }
}
