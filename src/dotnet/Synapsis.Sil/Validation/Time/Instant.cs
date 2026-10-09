using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Synapsis.Sil.Validation.Time;

/// <summary>
/// Reads the one time format SIL accepts: a UTC instant such as "2023-06-02T00:00:00Z", optionally with up to six
/// digits of fractions of a second ("2023-06-02T07:10:09.5Z"). No offsets, no local times.
/// </summary>
public static class Instant
{
    /// <summary>The format: year-month-dayThour:minute:second, optional .fraction, then Z.</summary>
    private static readonly Regex Format = new Regex(
        "^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\\.([0-9]{1,6}))?Z$",
        RegexOptions.Compiled);

    /// <summary>
    /// Reads an instant. Returns false when the text is not in the format, or names a moment that does not exist
    /// (30 February, hour 24).
    /// </summary>
    public static bool TryParse(string text, out DateTime instant)
    {
        instant = default;
        Match match = Format.Match(text);
        if (!match.Success)
        {
            return false;
        }
        int year = Part(match, 1);
        int month = Part(match, 2);
        int day = Part(match, 3);
        int hour = Part(match, 4);
        int minute = Part(match, 5);
        int second = Part(match, 6);
        if (!Exists(year, month, day, hour, minute, second))
        {
            return false;
        }
        DateTime whole = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        instant = whole.AddTicks(FractionTicks(match.Groups[7].Value));
        return true;
    }

    /// <summary>One numeric group of the match.</summary>
    private static int Part(Match match, int group)
    {
        return int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>True when the date and time exist on the calendar and the clock.</summary>
    private static bool Exists(int year, int month, int day, int hour, int minute, int second)
    {
        if (year < 1 || month < 1 || month > 12)
        {
            return false;
        }
        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        return hour < 24 && minute < 60 && second < 60;
    }

    /// <summary>The fraction of a second as ticks (one tick is 100 nanoseconds); "" is zero.</summary>
    private static long FractionTicks(string fraction)
    {
        if (fraction.Length == 0)
        {
            return 0;
        }
        string sevenDigits = fraction.PadRight(7, '0');
        return long.Parse(sevenDigits, CultureInfo.InvariantCulture);
    }
}
