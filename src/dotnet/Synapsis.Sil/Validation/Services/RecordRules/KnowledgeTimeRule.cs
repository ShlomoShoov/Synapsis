using System;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records.Parts;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Time;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models.Requests;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 7: known_at (when the record became known) must be a real instant, not before the record started, and not in
/// the future: at most 5 minutes after the server received it, to allow for a producer's clock running ahead. This
/// keeps later knowledge out of the past, so forecasts can be judged on what was knowable at the time.
/// </summary>
public sealed class KnowledgeTimeRule : IRecordRule
{
    /// <summary>How far a producer's clock may run ahead of the server's.</summary>
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Passes, or fails with E-SCHEMA for an impossible instant, E-KNOWN-BEFORE-OCCURRENCE or E-KNOWN-IN-FUTURE.</summary>
    public Result Check(RecordContext context)
    {
        string knownAtText = context.Record.KnownAt;
        if (!Instant.TryParse(knownAtText, out DateTime knownAt))
        {
            return Result.Failure(ErrorCodes.Schema, "known_at", $"'{knownAtText}' is not a real UTC instant");
        }
        ValidTime? time = context.ValidTime;
        if (time != null && Instant.TryParse(time.Start, out DateTime start) && knownAt < start)
        {
            return Result.Failure(ErrorCodes.KnownBeforeOccurrence, "known_at", "a record cannot be known before it started");
        }
        if (knownAt > context.ReceivedAt + FutureTolerance)
        {
            return Result.Failure(ErrorCodes.KnownInFuture, "known_at", "known_at is in the future");
        }
        return Result.Success();
    }
}
