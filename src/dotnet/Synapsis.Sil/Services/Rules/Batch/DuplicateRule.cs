using Synapsis.Sil.Services.Registry;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Batch;

/// <summary>
/// Rule 13, duplicates: the same record (same id from the same source) twice in one batch. An identical copy is
/// dropped and counted; a copy with different content is refused with E-ID-CONFLICT, because the batch contradicts
/// itself.
/// </summary>
public sealed class DuplicateRule : IBatchRule
{
    /// <summary>The first copy of each record passes; identical later copies are dropped; conflicting ones are refused.</summary>
    public Task<BatchRuleResult> CheckAsync(IReadOnlyList<CheckedRecord> records, IRegistry registry)
    {
        Dictionary<string, CheckedRecord> firstCopies = new Dictionary<string, CheckedRecord>();
        List<CheckedRecord> passed = new List<CheckedRecord>();
        List<Rejection> rejected = new List<Rejection>();
        int dropped = 0;
        foreach (CheckedRecord candidate in records)
        {
            string key = candidate.Record.Id + "|" + candidate.Record.Source;
            if (!firstCopies.TryGetValue(key, out CheckedRecord? first))
            {
                firstCopies[key] = candidate;
                passed.Add(candidate);
            }
            else if (first.Fingerprint == candidate.Fingerprint)
            {
                dropped++;
            }
            else
            {
                Problem conflict = new Problem(ErrorCodes.IdConflict, "id", "the same id appears twice in the batch with different content");
                rejected.Add(new Rejection(candidate.Index, candidate.Record.Id, conflict));
            }
        }
        return Task.FromResult(new BatchRuleResult(passed, rejected, dropped));
    }
}
