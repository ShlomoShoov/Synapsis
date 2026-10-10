using System;
using System.Threading;
using Synapsis.Sil.Services.Registry;
namespace Synapsis.Api.Services.Vocabulary;

/// <summary>
/// The vocabulary in force, held in memory. A registry is never changed after it is built, so replacing the
/// reference is enough: readers see either the old vocabulary or the new one, never a mix.
/// </summary>
public sealed class VocabularyProvider : IVocabularyProvider
{
    private IRegistry? _current;

    /// <inheritdoc/>
    public IRegistry Current
    {
        get
        {
            IRegistry? current = Volatile.Read(ref _current);
            if (current == null)
            {
                throw new InvalidOperationException("The vocabulary is not loaded yet: start-up has not finished.");
            }
            return current;
        }
    }

    /// <inheritdoc/>
    public void Replace(IRegistry registry)
    {
        Volatile.Write(ref _current, registry);
    }
}
