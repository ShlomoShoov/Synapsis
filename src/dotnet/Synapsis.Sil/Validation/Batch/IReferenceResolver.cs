using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// The two questions the batch rules ask about records already stored. The storage layer answers them from MongoDB
/// (one round trip each per batch); tests answer them with a fake.
/// </summary>
public interface IReferenceResolver
{
    /// <summary>For each of these entity ids that is stored, the entity types it is stored with.</summary>
    Task<Dictionary<string, HashSet<string>>> EntityTypesAsync(IReadOnlyCollection<string> entityIds);

    /// <summary>Which of these record ids (of any kind) are stored.</summary>
    Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds);
}
