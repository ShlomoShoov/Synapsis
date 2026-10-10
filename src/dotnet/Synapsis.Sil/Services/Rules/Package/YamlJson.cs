using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Synapsis.Sil.Services.Rules.Package;

/// <summary>
/// Turns YAML text (JSON is YAML too) into a JSON tree, so a JSON Schema can check it. Unquoted values are typed the
/// way YAML 1.2 types them: 95 is a number, true is a boolean, null and ~ are null; everything else, including NO
/// (Norway's code) and anything in quotes, stays text.
/// </summary>
public static class YamlJson
{
    /// <summary>An unquoted whole number, such as 95 or -3.</summary>
    private static readonly Regex Integer = new Regex("^[-+]?[0-9]+$", RegexOptions.Compiled);

    /// <summary>An unquoted decimal number, such as 0.5, 1e3 or -2.75.</summary>
    private static readonly Regex Decimal = new Regex("^[-+]?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)([eE][-+]?[0-9]+)?$", RegexOptions.Compiled);

    /// <summary>The first document of the text as JSON; null for an empty text. Throws YamlException for text that is not YAML.</summary>
    public static JsonNode? ToJson(string yaml)
    {
        YamlStream stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0)
        {
            return null;
        }
        return Convert(stream.Documents[0].RootNode);
    }

    /// <summary>One YAML node as JSON.</summary>
    private static JsonNode? Convert(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            return Object(mapping);
        }
        if (node is YamlSequenceNode sequence)
        {
            return Array(sequence);
        }
        return Scalar((YamlScalarNode)node);
    }

    /// <summary>A mapping as a JSON object, its keys as text.</summary>
    private static JsonObject Object(YamlMappingNode mapping)
    {
        JsonObject result = new JsonObject();
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            string key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
            result[key] = Convert(entry.Value);
        }
        return result;
    }

    /// <summary>A sequence as a JSON array.</summary>
    private static JsonArray Array(YamlSequenceNode sequence)
    {
        JsonArray result = new JsonArray();
        foreach (YamlNode item in sequence.Children)
        {
            result.Add(Convert(item));
        }
        return result;
    }

    /// <summary>A scalar: text when quoted; otherwise null, a boolean, a number or text.</summary>
    private static JsonNode? Scalar(YamlScalarNode scalar)
    {
        string text = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }
        if (text.Length == 0 || text == "~" || text == "null" || text == "Null" || text == "NULL")
        {
            return null;
        }
        if (text == "true" || text == "True" || text == "TRUE")
        {
            return JsonValue.Create(true);
        }
        if (text == "false" || text == "False" || text == "FALSE")
        {
            return JsonValue.Create(false);
        }
        if (Integer.IsMatch(text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
        {
            return JsonValue.Create(whole);
        }
        if (Decimal.IsMatch(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return JsonValue.Create(number);
        }
        return JsonValue.Create(text);
    }
}
