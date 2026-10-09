using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Checks one attribute value against its declaration, for <see cref="AttributeRule"/>. Returns what is wrong in plain
/// words, or null when the value is right.
/// </summary>
public static class AttributeValueCheck
{
    /// <summary>A calendar date written as 2024-01-31.</summary>
    private static readonly Regex DateFormat = new Regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.Compiled);

    /// <summary>What is wrong with the value, or null.</summary>
    public static string? Problem(JsonElement value, AttributeDefinition definition, IRegistry registry)
    {
        switch (definition.Type)
        {
            case AttributeType.String:
                return StringProblem(value, definition, registry);
            case AttributeType.StringList:
                return StringListProblem(value, definition, registry);
            case AttributeType.Integer:
                return IntegerProblem(value, definition);
            case AttributeType.Boolean:
                return BooleanProblem(value);
            default:
                return DateProblem(value);
        }
    }

    /// <summary>A text value, within its limits.</summary>
    private static string? StringProblem(JsonElement value, AttributeDefinition definition, IRegistry registry)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "must be a string";
        }
        return TextProblem(value.GetString()!, definition, registry);
    }

    /// <summary>A list of text values: at most MaxItems, each within the text limits.</summary>
    private static string? StringListProblem(JsonElement value, AttributeDefinition definition, IRegistry registry)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return "must be a list of strings";
        }
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return "must be a list of strings";
            }
        }
        if (definition.MaxItems != null && value.GetArrayLength() > definition.MaxItems)
        {
            return $"at most {definition.MaxItems} items";
        }
        foreach (JsonElement item in value.EnumerateArray())
        {
            string? problem = TextProblem(item.GetString()!, definition, registry);
            if (problem != null)
            {
                return problem;
            }
        }
        return null;
    }

    /// <summary>One text: its length in characters, its allowed values, its pattern.</summary>
    private static string? TextProblem(string text, AttributeDefinition definition, IRegistry registry)
    {
        if (definition.MaxLength != null && CountCharacters(text) > definition.MaxLength)
        {
            return $"longer than {definition.MaxLength} characters";
        }
        if (definition.AllowedValues != null && !definition.AllowedValues.Contains(text))
        {
            return "must be one of " + string.Join(", ", definition.AllowedValues);
        }
        if (definition.Pattern != null && !registry.Matches(definition.Pattern, text))
        {
            return $"does not match {definition.Pattern}";
        }
        return null;
    }

    /// <summary>A whole number (2024 and 2024.0 both are), within its range and allowed values.</summary>
    private static string? IntegerProblem(JsonElement value, AttributeDefinition definition)
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
    private static string? BooleanProblem(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return null;
        }
        return "must be true or false";
    }

    /// <summary>A real calendar date written as 2024-01-31.</summary>
    private static string? DateProblem(JsonElement value)
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

    /// <summary>The number of characters as people count them (a character outside the basic range counts once).</summary>
    private static int CountCharacters(string text)
    {
        int count = 0;
        foreach (Rune _ in text.EnumerateRunes())
        {
            count++;
        }
        return count;
    }
}
