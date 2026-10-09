using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Synapsis.Sil.Fingerprinting.Services.Domain;

/// <summary>
/// Writes JSON in one fixed form, so the same content always gives the same bytes: object keys in sorted order, no
/// spaces, every value exactly as received. Two records that differ only in key order or spacing come out identical.
/// </summary>
public static class CanonicalJson
{
    /// <summary>The canonical UTF-8 bytes of a JSON value.</summary>
    public static byte[] ToBytes(JsonElement value)
    {
        return ToBytes(value, Array.Empty<string>());
    }

    /// <summary>
    /// The canonical UTF-8 bytes of a JSON object, leaving out the named top-level fields
    /// (for example known_at, which is not part of a record's content).
    /// </summary>
    public static byte[] ToBytes(JsonElement value, IReadOnlyCollection<string> fieldsToLeaveOut)
    {
        using MemoryStream stream = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, value, fieldsToLeaveOut);
        }
        return stream.ToArray();
    }

    /// <summary>Writes one value: an object with sorted keys, an array in order, anything else as received.</summary>
    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, IReadOnlyCollection<string> fieldsToLeaveOut)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            WriteObject(writer, value, fieldsToLeaveOut);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            WriteArray(writer, value);
        }
        else
        {
            value.WriteTo(writer);
        }
    }

    /// <summary>Writes an object with its keys in sorted (ordinal) order, skipping the named fields.</summary>
    private static void WriteObject(Utf8JsonWriter writer, JsonElement value, IReadOnlyCollection<string> fieldsToLeaveOut)
    {
        List<JsonProperty> properties = new List<JsonProperty>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!Contains(fieldsToLeaveOut, property.Name))
            {
                properties.Add(property);
            }
        }
        properties.Sort(CompareByName);
        writer.WriteStartObject();
        foreach (JsonProperty property in properties)
        {
            writer.WritePropertyName(property.Name);
            WriteValue(writer, property.Value, Array.Empty<string>());
        }
        writer.WriteEndObject();
    }

    /// <summary>Writes an array, its items in their original order.</summary>
    private static void WriteArray(Utf8JsonWriter writer, JsonElement value)
    {
        writer.WriteStartArray();
        foreach (JsonElement item in value.EnumerateArray())
        {
            WriteValue(writer, item, Array.Empty<string>());
        }
        writer.WriteEndArray();
    }

    /// <summary>Orders two properties by name, comparing characters by their code (the same order everywhere).</summary>
    private static int CompareByName(JsonProperty first, JsonProperty second)
    {
        return string.CompareOrdinal(first.Name, second.Name);
    }

    /// <summary>True when the name is in the collection.</summary>
    private static bool Contains(IReadOnlyCollection<string> names, string name)
    {
        foreach (string candidate in names)
        {
            if (candidate == name)
            {
                return true;
            }
        }
        return false;
    }
}
