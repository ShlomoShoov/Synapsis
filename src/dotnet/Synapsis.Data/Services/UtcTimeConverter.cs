using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Data.Services;

/// <summary>
/// Reads a record's times (ISO 8601 text) as UTC instants. SIL times are UTC, and a date such as "2024-01-01" (a day's
/// precision) has no offset: .NET alone would read it in the machine's time zone, so the same record would give a
/// different instant on a laptop than in Docker. A time with an offset keeps its instant. Text that is not an ISO time
/// is refused, as before.
/// </summary>
public sealed class UtcTimeConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>The time as a UTC instant; a time without an offset is taken as UTC.</summary>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out DateTime time))
        {
            throw new JsonException("A time must be ISO 8601 text, such as \"2024-01-01\" or \"2024-01-01T10:00:00Z\".");
        }
        if (time.Kind == DateTimeKind.Unspecified)
        {
            time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
        }
        return new DateTimeOffset(time.ToUniversalTime());
    }

    /// <summary>Writes the time as ISO 8601 text.</summary>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
