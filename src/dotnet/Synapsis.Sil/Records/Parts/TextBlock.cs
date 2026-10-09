namespace Synapsis.Sil.Records.Parts;

/// <summary>
/// The words of a record: what semantic search reads. At least one of Title and Body is present.
/// </summary>
public sealed class TextBlock
{
    /// <summary>A short title, for example a vulnerability's name or a news headline.</summary>
    public string? Title { get; init; }

    /// <summary>A longer description.</summary>
    public string? Body { get; init; }

    /// <summary>The language, as a two- or three-letter code such as "en".</summary>
    public string? Lang { get; init; }
}
