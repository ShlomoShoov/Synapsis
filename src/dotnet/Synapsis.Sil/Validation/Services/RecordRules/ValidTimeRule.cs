using System;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;
using Synapsis.Sil.Validation.Services.Time;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 6: the valid time (when it happened) must be real and honest: the start is a real UTC instant aligned to its
/// precision (a "month" starts on the 1st), and an end, when given, comes after the start.
/// </summary>
public sealed class ValidTimeRule : IRecordRule
{
    /// <summary>Passes, or fails with E-SCHEMA for an impossible instant, E-TIME-ALIGN or E-TIME-ORDER.</summary>
    public Result Check(RecordContext context)
    {
        ValidTime? time = context.ValidTime;
        if (time == null)
        {
            return Result.Success();
        }
        if (!Instant.TryParse(time.Start, out DateTime start))
        {
            return Result.Failure(ErrorCodes.Schema, "time.start", $"'{time.Start}' is not a real UTC instant");
        }
        if (!context.Registry.IsPrecision(time.Precision) || !PrecisionAlignment.IsAligned(start, time.Precision))
        {
            return Result.Failure(ErrorCodes.TimeAlign, "time.start", $"start {time.Start} is not aligned to precision '{time.Precision}'");
        }
        return CheckEnd(time, start);
    }

    /// <summary>When the time has an end, it must be a real instant after the start.</summary>
    private static Result CheckEnd(ValidTime time, DateTime start)
    {
        if (time.End == null)
        {
            return Result.Success();
        }
        if (!Instant.TryParse(time.End, out DateTime end))
        {
            return Result.Failure(ErrorCodes.Schema, "time.end", $"'{time.End}' is not a real UTC instant");
        }
        if (end <= start)
        {
            return Result.Failure(ErrorCodes.TimeOrder, "time.end", "end must be after start");
        }
        return Result.Success();
    }
}
