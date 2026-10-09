using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Storage.Records.Models.Domain;
using Synapsis.Storage.Records.Models.Entities;

namespace Synapsis.Storage.Records.Services.Mappers;

/// <summary>Converts between the domain's record versions and the stored documents.</summary>
public static class RecordDocumentMapper
{
    /// <summary>JSON as plain values (numbers as numbers), the way the record arrived.</summary>
    private static readonly JsonWriterSettings PlainJson = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    /// <summary>A version as the document to store.</summary>
    public static RecordDocument ToDocument(RecordVersion version)
    {
        return new RecordDocument
        {
            Key = $"{version.Key.Id}|{version.Key.Source}|{version.Version}",
            Id = version.Key.Id,
            Source = version.Key.Source,
            Kind = version.Kind.ToString().ToLowerInvariant(),
            Version = version.Version,
            KnownAt = version.KnownAt,
            Fingerprint = version.Fingerprint,
            BatchId = version.BatchId,
            Producer = version.Producer,
            ReceivedAt = version.ReceivedAt,
            Record = BsonDocument.Parse(version.Record.GetRawText()),
        };
    }

    /// <summary>The statement a document belongs to.</summary>
    public static StatementKey ToKey(RecordDocument document)
    {
        return new StatementKey { Id = document.Id, Source = document.Source };
    }

    /// <summary>What version planning needs to know about a stored document.</summary>
    public static StoredVersion ToStoredVersion(RecordDocument document)
    {
        return new StoredVersion { Version = document.Version, Fingerprint = document.Fingerprint, KnownAt = document.KnownAt };
    }

    /// <summary>A stored document as a read returns it.</summary>
    public static StoredStatement ToStoredStatement(RecordDocument document)
    {
        using JsonDocument record = JsonDocument.Parse(document.Record.ToJson(PlainJson));
        return new StoredStatement
        {
            Record = record.RootElement.Clone(),
            Version = document.Version,
            BatchId = document.BatchId,
            Producer = document.Producer,
            ReceivedAt = document.ReceivedAt,
        };
    }
}
