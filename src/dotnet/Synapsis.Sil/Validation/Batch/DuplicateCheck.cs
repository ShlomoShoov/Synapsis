using System.Collections.Generic;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// Rule 13, first part: the same record (same id from the same source) twice in one batch. An identical copy is
/// counted and dropped; a copy with different content is refused with E-ID-CONFLICT, because the batch contradicts
/// itself.
/// </summary>
public static class DuplicateCheck
{
    /// <summary>
    /// Returns the first copy of each record. Identical later copies are counted in <paramref name="duplicates"/>;
    /// conflicting later copies are added to <paramref name="rejected"/>.
    /// </summary>
    public static List<PreparedRecord> KeepFirstCopies(List<PreparedRecord> prepared, List<Rejection> rejected,
        out int duplicates)
    {
        duplicates = 0;
        Dictionary<string, PreparedRecord> firstCopies = new Dictionary<string, PreparedRecord>();
        List<PreparedRecord> unique = new List<PreparedRecord>();
        foreach (PreparedRecord candidate in prepared)
        {
            string key = candidate.Record.Id + "|" + candidate.Record.Source;
            if (!firstCopies.TryGetValue(key, out PreparedRecord? first))
            {
                firstCopies[key] = candidate;
                unique.Add(candidate);
            }
            else if (first.Fingerprint == candidate.Fingerprint)
            {
                duplicates++;
            }
            else
            {
                RecordError conflict = new RecordError(ErrorCodes.IdConflict, "id",
                    "the same id appears twice in the batch with different content");
                rejected.Add(new Rejection(candidate.Index, candidate.Record.Id, conflict));
            }
        }
        return unique;
    }
}
