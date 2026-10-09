using System;
using System.Security.Cryptography;
using System.Text.Json;
using Synapsis.Sil.Fingerprinting.Abstractions;

namespace Synapsis.Sil.Fingerprinting.Services.Domain;

/// <summary>
/// The fingerprint as a SHA-256 of the record's canonical JSON (<see cref="CanonicalJson"/>: keys sorted, no spaces,
/// values as received), leaving known_at and known_at_basis out.
/// </summary>
public sealed class Fingerprinter : IFingerprinter
{
    /// <summary>The fields that are not part of a record's content.</summary>
    private static readonly string[] KnowledgeTimeFields = { "known_at", "known_at_basis" };

    /// <inheritdoc/>
    public string OfRecord(JsonElement record)
    {
        byte[] canonical = CanonicalJson.ToBytes(record, KnowledgeTimeFields);
        byte[] digest = SHA256.HashData(canonical);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
