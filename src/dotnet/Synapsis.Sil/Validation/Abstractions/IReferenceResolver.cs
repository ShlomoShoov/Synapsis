using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synapsis.Sil.Validation.Abstractions;

/// <summary>
/// What validation needs to know about records already stored: the two questions the reference rule asks, once per
/// batch. The storage layer answers them from MongoDB; tests answer them with a fake.
/// </summary>
public interface IReferenceResolver
{
    /// <summary>For each of these entity ids that is stored, the entity types it is stored with.</summary>
    Task<Dictionary<string, HashSet<string>>> EntityTypesAsync(IReadOnlyCollection<string> entityIds);

    /// <summary>Which of these record ids (of any kind) are stored.</summary>
    Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds);
}
