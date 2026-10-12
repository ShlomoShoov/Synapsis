using System.Collections.Generic;
using Synapsis.Api.Models.Results;
using Synapsis.Data.Models.Entities;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 3 of ingestion, change detection. Each record is compared with every stored state of the same record (same id,
/// same source): not stored yet → state 1; the same content hash as one of the states → unchanged (that state's
/// number); other content → the latest state + 1. Comparing with every state means a source that sends an old value
/// again (a bank's old name with its old reports) does not add a state each time.
/// </summary>
public static class ChangeDetector
{
    /// <summary>
    /// Sets the state (and the document id) of each record and splits them into "to save" and "unchanged".
    /// <paramref name="storedStates"/> holds every stored state of each record, by key "{id}|{source}".
    /// </summary>
    public static ChangeResult Detect(IReadOnlyList<StoredRecord> records, IReadOnlyDictionary<string, List<StoredRecord>> storedStates)
    {
        ChangeResult changes = new ChangeResult();
        foreach (StoredRecord record in records)
        {
            string key = StoredRecord.KeyOf(record.RecordId, record.Source);
            List<StoredRecord> states = new List<StoredRecord>();
            if (storedStates.ContainsKey(key))
            {
                states = storedStates[key];
            }

            // Look for a stored state with the same content, and for the highest state number.
            StoredRecord? sameContent = null;
            int latestState = 0;
            foreach (StoredRecord state in states)
            {
                if (state.ContentHash == record.ContentHash)
                {
                    sameContent = state;
                }
                if (state.State > latestState)
                {
                    latestState = state.State;
                }
            }

            if (sameContent != null)
            {
                record.State = sameContent.State;
                changes.Unchanged.Add(record);
            }
            else
            {
                record.State = latestState + 1;
                changes.ToSave.Add(record);
            }
            record.Key = key + "|" + record.State;
        }
        return changes;
    }
}
