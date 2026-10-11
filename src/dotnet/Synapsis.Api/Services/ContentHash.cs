using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Synapsis.Api.Services;

/// <summary>
/// The fingerprint of a record's content: SHA-256 of its JSON without known_at and known_at_basis, so a record sent
/// again with only a later known_at counts as unchanged. Fields are hashed in the order they arrive (the feeders
/// always build a record the same way).
/// </summary>
public static class ContentHash
{
    /// <summary>The hash as 64 lowercase hex characters.</summary>
    public static string Of(JsonElement record)
    {
        // 1. Write the record again without the two knowledge-time fields.
        using MemoryStream content = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(content))
        {
            writer.WriteStartObject();
            foreach (JsonProperty field in record.EnumerateObject())
            {
                if (field.Name == "known_at" || field.Name == "known_at_basis")
                {
                    continue;
                }
                field.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        // 2. Hash what was written.
        byte[] hash = SHA256.HashData(content.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
