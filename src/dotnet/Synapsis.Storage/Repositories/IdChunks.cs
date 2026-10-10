using System.Collections.Generic;
using Synapsis.Sil.Models.Versioning;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// Cuts long lists of ids into chunks, so one query never names more than <see cref="ChunkSize"/> ids.
/// </summary>
public static class IdChunks
{
    /// <summary>The most ids one query names.</summary>
    public const int ChunkSize = 20000;

    /// <summary>The distinct record ids of the statements.</summary>
    public static List<string> IdsOf(IReadOnlyCollection<StatementKey> keys)
    {
        HashSet<string> ids = new HashSet<string>();
        foreach (StatementKey key in keys)
        {
            ids.Add(key.Id);
        }
        return new List<string>(ids);
    }

    /// <summary>The ids in chunks of at most <see cref="ChunkSize"/>.</summary>
    public static List<List<string>> Chunks(IReadOnlyCollection<string> ids)
    {
        List<List<string>> chunks = new List<List<string>>();
        List<string> current = new List<string>();
        foreach (string id in ids)
        {
            current.Add(id);
            if (current.Count == ChunkSize)
            {
                chunks.Add(current);
                current = new List<string>();
            }
        }
        if (current.Count > 0)
        {
            chunks.Add(current);
        }
        return chunks;
    }
}
