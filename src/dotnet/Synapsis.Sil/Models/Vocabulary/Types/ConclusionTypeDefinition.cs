namespace Synapsis.Sil.Models.Vocabulary.Types;

/// <summary>
/// A conclusion type, for example classification, spark or forecast.
/// </summary>
public sealed class ConclusionTypeDefinition
{
    /// <summary>The type code in lower case, for example "classification".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A human title.</summary>
    public string Title { get; init; } = string.Empty;
}
