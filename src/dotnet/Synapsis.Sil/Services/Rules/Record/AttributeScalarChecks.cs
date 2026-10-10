using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Vocabulary.Attributes;

namespace Synapsis.Sil.Services.Rules.Record;

/// <summary>
/// The checks of single-value attributes (integer, boolean, date): each returns the problem, or null when the value fits.
/// </summary>
public static class AttributeScalarChecks
{
    /// <summary>A calendar date written as 2024-01-31.</summary>
    public static readonly Regex DateFormat = new Regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.Compiled);

    /// <summary>A whole number (2024 and 2024.0 both are), within its range and allowed values.</summary>
    public static string? IntegerProblem(JsonElement value, AttributeDefinition definition)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return "must be an integer";
        }
        double number = value.GetDouble();
        if (Math.Floor(number) != number)
        {
            return "must be an integer";
        }
        bool belowMin = definition.Min != null && number < definition.Min;
        bool aboveMax = definition.Max != null && number > definition.Max;
        if (belowMin || aboveMax)
        {
            return $"must lie in [{definition.Min}, {definition.Max}]";
        }
        string asText = number.ToString(CultureInfo.InvariantCulture);
        if (definition.AllowedValues != null && !definition.AllowedValues.Contains(asText))
        {
            return "must be one of " + string.Join(", ", definition.AllowedValues);
        }
        return null;
    }

    /// <summary>true or false.</summary>
    public static string? BooleanProblem(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return null;
        }
        return "must be true or false";
    }

    /// <summary>A real calendar date written as 2024-01-31.</summary>
    public static string? DateProblem(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !DateFormat.IsMatch(value.GetString()!))
        {
            return "must be a date like 2024-01-31";
        }
        bool real = DateTime.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime _);
        if (!real)
        {
            return "is not a real date";
        }
        return null;
    }
}
