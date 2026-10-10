using System.Text;
using System.Text.Json;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Services.Registry;
namespace Synapsis.Sil.Services.Rules.Record;

/// <summary>
/// Checks one attribute value against its declaration, for <see cref="AttributeRule"/>. Returns what is wrong in plain
/// words, or null when the value is right.
/// </summary>
public static class AttributeValueCheck
{
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
                return AttributeScalarChecks.IntegerProblem(value, definition);
            case AttributeType.Boolean:
                return AttributeScalarChecks.BooleanProblem(value);
            default:
                return AttributeScalarChecks.DateProblem(value);
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
