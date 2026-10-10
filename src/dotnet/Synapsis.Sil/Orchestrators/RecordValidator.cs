using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Reading;
using Synapsis.Sil.Services.Rules.Envelope;
using Synapsis.Sil.Services.Rules.Record;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Orchestrators;

/// <summary>
/// Validates one record in three stages: the envelope rules on the raw JSON (rules 1–2), reading it into its typed
/// class, then the record rules (3–12). Each stage stops at the first failure, so a record has at most one problem.
/// The rules and their order come in through the constructor (see SilComponents).
/// </summary>
public sealed class RecordValidator : IRecordValidator
{
    private readonly IReadOnlyList<IEnvelopeRule> _envelopeRules;
    private readonly IRecordReader _reader;
    private readonly IReadOnlyList<IRecordRule> _recordRules;

    /// <summary>Creates the validator from its parts, the rules in the order they must run.</summary>
    public RecordValidator(IEnumerable<IEnvelopeRule> envelopeRules, IRecordReader reader, IEnumerable<IRecordRule> recordRules)
    {
        _envelopeRules = new List<IEnvelopeRule>(envelopeRules);
        _reader = reader;
        _recordRules = new List<IRecordRule>(recordRules);
    }

    /// <inheritdoc/>
    public Result<SilRecord> Validate(RecordValidationRequest request)
    {
        foreach (IEnvelopeRule rule in _envelopeRules)
        {
            Result result = rule.Check(request.Raw);
            if (!result.Succeeded)
            {
                return Result<SilRecord>.Failure(result.Problem!);
            }
        }
        Result<SilRecord> read = _reader.Read(request.Raw);
        if (!read.Succeeded)
        {
            return read;
        }
        RecordContext context = new RecordContext(read.Value!, request.Registry, request.ReceivedAt);
        foreach (IRecordRule rule in _recordRules)
        {
            Result result = rule.Check(context);
            if (!result.Succeeded)
            {
                return Result<SilRecord>.Failure(result.Problem!);
            }
        }
        return read;
    }
}
