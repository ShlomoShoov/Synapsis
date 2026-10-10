using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Synapsis.Sil.Services.Fingerprinting;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Rules.Batch;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Orchestrators;

/// <summary>
/// Validates a batch in two stages: every record on its own (the record validator, plus its fingerprint), then the
/// batch rules in order (duplicates, references), each receiving the records the previous one passed on.
/// </summary>
public sealed class BatchValidator : IBatchValidator
{
    private readonly IRecordValidator _recordValidator;
    private readonly IFingerprinter _fingerprinter;
    private readonly IReadOnlyList<IBatchRule> _batchRules;

    /// <summary>Creates the validator from its parts, the batch rules in the order they must run.</summary>
    public BatchValidator(IRecordValidator recordValidator, IFingerprinter fingerprinter, IEnumerable<IBatchRule> batchRules)
    {
        _recordValidator = recordValidator;
        _fingerprinter = fingerprinter;
        _batchRules = new List<IBatchRule>(batchRules);
    }

    /// <inheritdoc/>
    public async Task<BatchValidationResult> ValidateAsync(BatchValidationRequest request)
    {
        List<Rejection> rejected = new List<Rejection>();
        IReadOnlyList<CheckedRecord> running = CheckEachRecord(request, rejected);
        int duplicates = 0;
        foreach (IBatchRule rule in _batchRules)
        {
            BatchRuleResult result = await rule.CheckAsync(new BatchRuleRequest(running, request.Registry));
            rejected.AddRange(result.Rejected);
            duplicates += result.Dropped;
            running = result.Passed;
        }
        rejected.Sort(CompareByIndex);
        return new BatchValidationResult(running, rejected, duplicates);
    }

    /// <summary>Validates every record on its own; returns the ones that pass, with their fingerprints.</summary>
    private List<CheckedRecord> CheckEachRecord(BatchValidationRequest request, List<Rejection> rejected)
    {
        List<CheckedRecord> passed = new List<CheckedRecord>();
        for (int index = 0; index < request.Records.Count; index++)
        {
            JsonElement raw = request.Records[index];
            Result<SilRecord> result = _recordValidator.Validate(new RecordValidationRequest(raw, request.Registry, request.ReceivedAt));
            if (!result.Succeeded)
            {
                rejected.Add(new Rejection(index, IdOf(raw), result.Problem!));
                continue;
            }
            passed.Add(new CheckedRecord(index, raw, result.Value!, _fingerprinter.OfRecord(raw)));
        }
        return passed;
    }

    /// <summary>The record's "id" when it has one as text (a refused record may not).</summary>
    private static string? IdOf(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }
        return null;
    }

    /// <summary>Orders rejections by batch position.</summary>
    private static int CompareByIndex(Rejection first, Rejection second)
    {
        return first.Index - second.Index;
    }
}
