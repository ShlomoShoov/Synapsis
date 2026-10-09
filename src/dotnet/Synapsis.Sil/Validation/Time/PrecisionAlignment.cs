using System;

namespace Synapsis.Sil.Validation.Time;

/// <summary>
/// Whether a start time sits on the boundary of its precision. A record must not claim more precision than it has:
/// a "month" starts on the 1st at midnight, a "week" on a Monday at midnight, a "quarter" on 1 January, April, July
/// or October.
/// </summary>
public static class PrecisionAlignment
{
    /// <summary>True when <paramref name="start"/> is aligned to <paramref name="precision"/>; false for an unknown precision.</summary>
    public static bool IsAligned(DateTime start, string precision)
    {
        switch (precision)
        {
            case "second":
                return start.Ticks % TimeSpan.TicksPerSecond == 0;
            case "minute":
                return start.Ticks % TimeSpan.TicksPerMinute == 0;
            case "hour":
                return start.Ticks % TimeSpan.TicksPerHour == 0;
            case "day":
                return IsMidnight(start);
            case "week":
                return IsMidnight(start) && start.DayOfWeek == DayOfWeek.Monday;
            case "month":
                return IsMidnight(start) && start.Day == 1;
            case "quarter":
                return IsMidnight(start) && start.Day == 1 && start.Month % 3 == 1;
            case "year":
                return IsMidnight(start) && start.Day == 1 && start.Month == 1;
            default:
                return false;
        }
    }

    /// <summary>True when the time is exactly midnight (the start of a day).</summary>
    private static bool IsMidnight(DateTime time)
    {
        return time.TimeOfDay == TimeSpan.Zero;
    }
}
