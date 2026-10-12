namespace Synapsis.Data.Models.Options;

/// <summary>
/// Where the embedder service is and which of its models turns text into vectors, from the configuration section
/// "Embedder" (sharedsettings.json; environment variable Embedder__Url overrides it). The indexer and the search
/// must use the same model, so they share these settings.
/// </summary>
public sealed class EmbedderOptions
{
    /// <summary>The address, such as "http://embedder:8090".</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The model's name in the embedder, such as "search".</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>How many numbers a vector of this model has (the size of the vector field in Elasticsearch).</summary>
    public int Dimensions { get; set; }

    /// <summary>The longest text the embedder accepts, in characters; a longer text is cut to this length.</summary>
    public int MaxTextLength { get; set; }
}
