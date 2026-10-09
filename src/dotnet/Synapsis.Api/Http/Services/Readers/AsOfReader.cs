using System;
using System.Globalization;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Time;

namespace Synapsis.Api.Http.Services.Readers;

/// <summary>
/// Reads the as_of query parameter: a date ("2024-05-01") means the end of that day; a SIL instant
/// ("2024-05-01T12:00:00Z") is taken as given; no value means the latest. Anything else is E-PARAM.
/// </summary>
public static class AsOfReader
{
    /// <summary>The moment asked for (UTC), or null for the latest.</summary>
    public static Result<DateTime?> Read(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Result<DateTime?>.Success(null);
        }
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime day))
        {
            DateTime endOfDay = DateTime.SpecifyKind(day.Date.AddDays(1).AddMilliseconds(-1), DateTimeKind.Utc);
            return Result<DateTime?>.Success(endOfDay);
        }
        if (Instant.TryParse(text, out DateTime instant))
        {
            return Result<DateTime?>.Success(instant);
        }
        return Result<DateTime?>.Failure(ErrorCodes.Param, "as_of", "as_of must be a date (2024-05-01) or a UTC instant (2024-05-01T12:00:00Z)");
    }
}
