using System.Collections.Generic;
using Synapsis.Api.Models;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 3 of ingestion, change detection. Each record is compared with the latest stored state of the same record
/// (same id, same source): not stored yet → state 1; same content hash → unchanged; other content → the next state.
/// </summary>
public static class ChangeDetector
{
    /// <summary>
    /// Sets the state (and the document id) of each record and splits them into "to save" and "unchanged".
    /// <paramref name="latestStates"/> holds the latest stored state of each record, by key "{id}|{source}".
    /// </summary>
    public static ChangeResult Detect(IReadOnlyList<StoredRecord> records, IReadOnlyDictionary<string, StoredRecord> latestStates)
    {
        ChangeResult changes = new ChangeResult();
        foreach (StoredRecord record in records)
        {
            string key = StoredRecord.KeyOf(record.RecordId, record.Source);
            if (!latestStates.TryGetValue(key, out StoredRecord? latest))
            {
                record.State = 1;
                changes.ToSave.Add(record);
            }
            else if (latest.ContentHash == record.ContentHash)
            {
                record.State = latest.State;
                changes.Unchanged.Add(record);
            }
            else
            {
                record.State = latest.State + 1;
                changes.ToSave.Add(record);
            }
            record.Key = key + "|" + record.State;
        }
        return changes;
    }
}
