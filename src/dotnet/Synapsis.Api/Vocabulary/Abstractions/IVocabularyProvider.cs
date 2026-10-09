using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Api.Vocabulary.Abstractions;

/// <summary>
/// Holds the vocabulary in force: the registry built from every current schema package. A batch takes it once and
/// keeps it to the end, so a package registered meanwhile never changes the rules halfway through a batch.
/// </summary>
public interface IVocabularyProvider
{
    /// <summary>The vocabulary in force. Start-up loads it before the server accepts requests.</summary>
    IRegistry Current { get; }

    /// <summary>Puts a new vocabulary in force, as a whole.</summary>
    void Replace(IRegistry registry);
}
