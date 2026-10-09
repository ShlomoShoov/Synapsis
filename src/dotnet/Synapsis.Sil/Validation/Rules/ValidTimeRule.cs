using System;
using Synapsis.Sil.Records.Parts;
using Synapsis.Sil.Validation.Time;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 6: the valid time (when it happened) must be real and honest: the start is a real UTC instant aligned to its
/// precision (a "month" starts on the 1st), and an end, when given, comes after the start.
/// </summary>
public sealed class ValidTimeRule : IRecordRule
{
    /// <summary>Returns E-SCHEMA for an impossible instant, E-TIME-ALIGN, E-TIME-ORDER, or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        ValidTime? time = context.ValidTime;
        if (time == null)
        {
            return null;
        }
        if (!Instant.TryParse(time.Start, out DateTime start))
        {
            return new RecordError(ErrorCodes.Schema, "time.start", $"'{time.Start}' is not a real UTC instant");
        }
        bool knownPrecision = context.Registry.Precisions.Contains(time.Precision);
        if (!knownPrecision || !PrecisionAlignment.IsAligned(start, time.Precision))
        {
            return new RecordError(ErrorCodes.TimeAlign, "time.start",
                $"start {time.Start} is not aligned to precision '{time.Precision}'");
        }
        return CheckEnd(time, start);
    }

    /// <summary>When the time has an end, it must be a real instant after the start.</summary>
    private static RecordError? CheckEnd(ValidTime time, DateTime start)
    {
        if (time.End == null)
        {
            return null;
        }
        if (!Instant.TryParse(time.End, out DateTime end))
        {
            return new RecordError(ErrorCodes.Schema, "time.end", $"'{time.End}' is not a real UTC instant");
        }
        if (end <= start)
        {
            return new RecordError(ErrorCodes.TimeOrder, "time.end", "end must be after start");
        }
        return null;
    }
}
