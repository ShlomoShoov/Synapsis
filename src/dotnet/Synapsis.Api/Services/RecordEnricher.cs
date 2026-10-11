using System;
using System.Collections.Generic;
using MongoDB.Bson;
using Synapsis.Api.Models;
using Synapsis.Api.Models.Records;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 2 of ingestion, enrichment: turns each valid record into the document MongoDB will hold, with its content
/// hash and where it came from. The state is not known yet; step 3 sets it.
/// </summary>
public static class RecordEnricher
{
    /// <summary>One stored record for each valid record, in the same order.</summary>
    public static List<StoredRecord> Enrich(IReadOnlyList<SilRecord> validRecords, IngestBatch batch, DateTime receivedAt)
    {
        List<StoredRecord> storedRecords = new List<StoredRecord>();
        foreach (SilRecord record in validRecords)
        {
            StoredRecord stored = new StoredRecord();
            stored.RecordId = record.Id!;
            stored.Source = record.Source!;
            stored.Kind = record.Kind!;
            stored.Type = record.Type!;
            stored.KnownAt = record.KnownAt!.Value.UtcDateTime;
            stored.ContentHash = ContentHash.Of(record.Json);
            stored.BatchId = batch.BatchId;
            stored.Producer = batch.Producer;
            stored.ReceivedAt = receivedAt;
            stored.Record = BsonDocument.Parse(record.Json.GetRawText());
            storedRecords.Add(stored);
        }
        return storedRecords;
    }
}
