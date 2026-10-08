using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Synapsis.Sil.Registry.Definitions;

namespace Synapsis.Sil.Registry;

/// <summary>
/// One attribute a type declares: its type, limits, allowed values and pattern, with the
/// pattern compiled once per registry revision.
/// </summary>
public sealed class AttributeRule
{
    private static readonly HashSet<string> KnownTypes =
        ["string", "string_list", "integer", "number", "boolean", "date", "instant"];

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    public required string Type { get; init; }
    public bool Required { get; init; }
    public IReadOnlySet<string>? Enum { get; init; }
    public Regex? Pattern { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public int? MaxItems { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }

    /// <summary>Compiles one attribute declaration, e.g. <c>{type: string, enum: [Known, Unknown]}</c>.</summary>
    public static AttributeRule Compile(JsonObject definition)
    {
        var type = Text(definition["type"]) ?? throw new FormatException("an attribute has no type");
        if (!KnownTypes.Contains(type))
            throw new FormatException($"unknown attribute type '{type}'");

        Regex? pattern = null;
        if (Text(definition["pattern"]) is { } source)
        {
            try
            {
                pattern = new Regex(source, RegexOptions.CultureInvariant, PatternTimeout);
            }
            catch (ArgumentException ex)
            {
                throw new FormatException($"invalid attribute pattern '{source}'", ex);
            }
        }

        return new AttributeRule
        {
            Type = type,
            Required = IsTrue(definition["required"]),
            Enum = definition["enum"] is JsonArray values ? Strings(values).ToHashSet(StringComparer.Ordinal) : null,
            Pattern = pattern,
            MinLength = (int?)Number(definition["min_length"]),
            MaxLength = (int?)Number(definition["max_length"]),
            MaxItems = (int?)Number(definition["max_items"]),
            Min = Number(definition["min"]),
            Max = Number(definition["max"]),
        };
    }

    /// <summary>Why <paramref name="value"/> breaks this rule, or null when it is valid.</summary>
    public string? Check(JsonElement value)
    {
        switch (Type)
        {
            case "string":
                return CheckString(value);
            case "string_list":
                if (value.ValueKind != JsonValueKind.Array)
                    return "must be a list of strings";
                var count = value.GetArrayLength();
                if (count > MaxItems)
                    return $"has {count} items; at most {MaxItems} allowed";
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (CheckString(item) is { } problem)
                        return $"item {index}: {problem}";
                    index++;
                }
                return null;
            case "integer":
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer)
                    ? CheckRange(integer)
                    : "must be an integer";
            case "number":
                return value.ValueKind == JsonValueKind.Number ? CheckRange(value.GetDouble()) : "must be a number";
            case "boolean":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "must be true or false";
            case "date":
                return value.ValueKind == JsonValueKind.String
                       && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                           DateTimeStyles.None, out _)
                    ? null
                    : "must be a date, yyyy-MM-dd";
            case "instant":
                return value.ValueKind == JsonValueKind.String
                       && DateTimeOffset.TryParseExact(value.GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'",
                           CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)
                    ? null
                    : "must be an instant, yyyy-MM-ddTHH:mm:ssZ";
            default:
                return $"unknown attribute type '{Type}'";
        }
    }

    private string? CheckString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            return "must be a string";
        var text = value.GetString()!;
        if (Enum is not null && !Enum.Contains(text))
            return $"'{text}' is not one of {string.Join(", ", Enum)}";
        if (text.Length < MinLength)
            return $"is shorter than {MinLength} characters";
        if (text.Length > MaxLength)
            return $"is longer than {MaxLength} characters";
        if (Pattern is not null)
        {
            try
            {
                if (!Pattern.IsMatch(text))
                    return $"does not match {Pattern}";
            }
            catch (RegexMatchTimeoutException)
            {
                return $"could not be matched against {Pattern} in time";
            }
        }
        return null;
    }

    private string? CheckRange(double number)
    {
        if (number < Min)
            return $"is below the minimum {Min}";
        if (number > Max)
            return $"is above the maximum {Max}";
        return null;
    }
}
