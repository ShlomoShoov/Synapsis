using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Synapsis.Storage.Records.Models.Entities;

/// <summary>
/// One document of the "records" collection: one version of one statement. It is an envelope around the record: the
/// fields queries need (id, source, kind, version, known_at as a date), where the version came from, and the record
/// itself, as stored.
/// </summary>
public sealed class RecordDocument
{
    /// <summary>"&lt;id&gt;|&lt;source&gt;|&lt;version&gt;": unique, so two writers cannot both store the same version.</summary>
    [BsonId]
    public string Key { get; set; } = string.Empty;

    /// <summary>The record id.</summary>
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The source that stated it.</summary>
    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>The record's kind: entity, event, relation or conclusion.</summary>
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The version number: 1, 2, ...</summary>
    [BsonElement("version")]
    public int Version { get; set; }

    /// <summary>When this version became known, as a date (text times can have fractions, so text would compare wrongly).</summary>
    [BsonElement("known_at")]
    public DateTime KnownAt { get; set; }

    /// <summary>The fingerprint of its content.</summary>
    [BsonElement("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The batch that brought it.</summary>
    [BsonElement("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>The producer that sent the batch.</summary>
    [BsonElement("producer")]
    public string Producer { get; set; } = string.Empty;

    /// <summary>When the server received it.</summary>
    [BsonElement("received_at")]
    public DateTime ReceivedAt { get; set; }

    /// <summary>The record as stored.</summary>
    [BsonElement("record")]
    public BsonDocument Record { get; set; } = new BsonDocument();
}
