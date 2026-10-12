using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Synapsis.Data.Models.Entities;

/// <summary>
/// One document of the MongoDB collection "records": one state of one record from one source. Around the record
/// itself (kept exactly as it arrived) are the few fields the queries need and where the record came from.
/// The same record id from two sources is two separate records, each with its own states.
/// </summary>
public sealed class StoredRecord
{
    /// <summary>The document id, "{id}|{source}|{state}": unique, so the same state can never be stored twice.</summary>
    [BsonId]
    public string Key { get; set; } = string.Empty;

    /// <summary>The record's id, such as "evt:kev:CVE-2021-44228".</summary>
    [BsonElement("id")]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>The source that stated it, such as "src:cisa:kev".</summary>
    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>The record's kind: entity, event, relation or conclusion.</summary>
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The record's type, such as "VULN.EXPLOITED".</summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The state number: how many times this record (same id, same source) has been stored with new content.
    /// 1 for the first copy, then 2, 3, ... each time its content changes. Earlier states are never changed.
    /// </summary>
    [BsonElement("state")]
    public int State { get; set; }

    /// <summary>When the source says this was known (the record's known_at), in UTC.</summary>
    [BsonElement("known_at")]
    public DateTime KnownAt { get; set; }

    /// <summary>The SHA-256 of the record without known_at: equal hashes mean equal content.</summary>
    [BsonElement("content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The batch that brought this state.</summary>
    [BsonElement("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Who sent the batch, such as "feeder:kev".</summary>
    [BsonElement("producer")]
    public string Producer { get; set; } = string.Empty;

    /// <summary>When the API received the batch, in UTC.</summary>
    [BsonElement("received_at")]
    public DateTime ReceivedAt { get; set; }

    /// <summary>The record exactly as it arrived.</summary>
    [BsonElement("record")]
    public BsonDocument Record { get; set; } = new BsonDocument();

    /// <summary>The key of a record without its state, "{id}|{source}": one record from one source.</summary>
    public static string KeyOf(string recordId, string source)
    {
        return recordId + "|" + source;
    }
}
