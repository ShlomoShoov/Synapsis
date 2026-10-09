using System;
using System.Security.Cryptography;
using System.Text.Json;
using Synapsis.Sil.Fingerprinting.Abstractions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Reading.Models.Configuration;

namespace Synapsis.Sil.Fingerprinting.Services.Domain;

/// <summary>
/// The fingerprint as a SHA-256 of canonical JSON (<see cref="CanonicalJson"/>: keys sorted, no spaces, values as
/// received). A record leaves known_at and known_at_basis out; a package is first written as JSON from its objects.
/// </summary>
public sealed class Fingerprinter : IFingerprinter
{
    /// <summary>The fields that are not part of a record's content.</summary>
    private static readonly string[] KnowledgeTimeFields = { "known_at", "known_at_basis" };

    /// <inheritdoc/>
    public string OfRecord(JsonElement record)
    {
        return Hash(CanonicalJson.ToBytes(record, KnowledgeTimeFields));
    }

    /// <inheritdoc/>
    public string OfPackage(SchemaPackage package)
    {
        JsonElement json = JsonSerializer.SerializeToElement(package, SilJsonOptions.Value);
        return Hash(CanonicalJson.ToBytes(json));
    }

    /// <summary>SHA-256 of the bytes, as 64 lower-case hex characters.</summary>
    private static string Hash(byte[] canonical)
    {
        byte[] digest = SHA256.HashData(canonical);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
