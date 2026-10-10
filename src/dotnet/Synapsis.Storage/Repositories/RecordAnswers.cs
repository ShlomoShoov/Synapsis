using System.Collections.Generic;
using Synapsis.Sil.Models.Versioning;
using Synapsis.Storage.Entities;
using Synapsis.Storage.Repositories.Mappers;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// Builds the answers of <see cref="RecordRepository"/> from the documents MongoDB returned.
/// </summary>
public static class RecordAnswers
{
    /// <summary>Adds the stored version of each document whose statement was asked for.</summary>
    public static void AddWanted(List<RecordDocument> documents, HashSet<StatementKey> wanted, Dictionary<StatementKey, List<StoredVersion>> found)
    {
        foreach (RecordDocument document in documents)
        {
            StatementKey key = RecordDocumentMapper.ToKey(document);
            if (!wanted.Contains(key))
            {
                continue;
            }
            if (!found.TryGetValue(key, out List<StoredVersion>? versions))
            {
                versions = new List<StoredVersion>();
                found[key] = versions;
            }
            versions.Add(RecordDocumentMapper.ToStoredVersion(document));
        }
    }

    /// <summary>Adds one entity type of an id.</summary>
    public static void AddType(Dictionary<string, HashSet<string>> types, string id, string type)
    {
        if (!types.TryGetValue(id, out HashSet<string>? known))
        {
            known = new HashSet<string>();
            types[id] = known;
        }
        known.Add(type);
    }

    /// <summary>The found versions as the read-only answer.</summary>
    public static IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> ReadOnly(Dictionary<StatementKey, List<StoredVersion>> found)
    {
        Dictionary<StatementKey, IReadOnlyList<StoredVersion>> answer = new Dictionary<StatementKey, IReadOnlyList<StoredVersion>>();
        foreach (KeyValuePair<StatementKey, List<StoredVersion>> entry in found)
        {
            answer[entry.Key] = entry.Value;
        }
        return answer;
    }
}
