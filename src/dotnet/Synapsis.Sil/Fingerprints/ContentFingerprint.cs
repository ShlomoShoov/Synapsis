using System;
using System.Security.Cryptography;
using System.Text.Json;

namespace Synapsis.Sil.Fingerprints;

/// <summary>
/// The fingerprint of a record's content: a SHA-256 of its canonical JSON, written as 64 lower-case hex characters.
/// Equal content gives an equal fingerprint, so comparing two short strings answers "same record, or changed?".
/// The content is everything except known_at and known_at_basis: re-sending a record with another knowledge time is
/// not a change.
/// </summary>
public static class ContentFingerprint
{
    /// <summary>The fields that are not part of a record's content.</summary>
    private static readonly string[] KnowledgeTimeFields = { "known_at", "known_at_basis" };

    /// <summary>The fingerprint of a raw record's content.</summary>
    public static string OfRecord(JsonElement record)
    {
        byte[] canonical = CanonicalJson.ToBytes(record, KnowledgeTimeFields);
        byte[] digest = SHA256.HashData(canonical);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
