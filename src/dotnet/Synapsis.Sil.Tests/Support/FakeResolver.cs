using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Validation.Abstractions;

namespace Synapsis.Sil.Tests.Support;

/// <summary>A stand-in for the store: it "holds" only the entities and records a test puts in it.</summary>
public sealed class FakeResolver : IReferenceResolver
{
    /// <summary>Stored entities and their types.</summary>
    public Dictionary<string, HashSet<string>> StoredEntities { get; } = new Dictionary<string, HashSet<string>>();

    /// <summary>Stored record ids of any kind.</summary>
    public HashSet<string> StoredIds { get; } = new HashSet<string>();

    /// <summary>The stored types of the asked entities.</summary>
    public Task<Dictionary<string, HashSet<string>>> EntityTypesAsync(IReadOnlyCollection<string> entityIds)
    {
        Dictionary<string, HashSet<string>> found = new Dictionary<string, HashSet<string>>();
        foreach (string id in entityIds)
        {
            if (StoredEntities.TryGetValue(id, out HashSet<string>? types))
            {
                found[id] = types;
            }
        }
        return Task.FromResult(found);
    }

    /// <summary>Which of the asked ids are stored.</summary>
    public Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds)
    {
        HashSet<string> found = new HashSet<string>();
        foreach (string id in recordIds)
        {
            if (StoredIds.Contains(id))
            {
                found.Add(id);
            }
        }
        return Task.FromResult(found);
    }
}
