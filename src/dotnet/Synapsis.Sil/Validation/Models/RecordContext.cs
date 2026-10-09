using System;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Validation.Models;

/// <summary>
/// What every record rule reads: the typed record, the vocabulary, and when the record arrived. Nothing in it
/// changes while the rules run, so the rules cannot affect each other.
/// </summary>
public sealed class RecordContext
{
    /// <summary>Creates the context of one record.</summary>
    public RecordContext(SilRecord record, IRegistry registry, DateTime receivedAt)
    {
        Record = record;
        Registry = registry;
        ReceivedAt = receivedAt;
    }

    /// <summary>The record being checked.</summary>
    public SilRecord Record { get; }

    /// <summary>The vocabulary to check it against.</summary>
    public IRegistry Registry { get; }

    /// <summary>When the server received the record (UTC).</summary>
    public DateTime ReceivedAt { get; }

    /// <summary>The record's valid time: an event's time, a relation's optional time, or null for other kinds.</summary>
    public ValidTime? ValidTime
    {
        get
        {
            if (Record is EventRecord eventRecord)
            {
                return eventRecord.Time;
            }
            if (Record is RelationRecord relation)
            {
                return relation.Time;
            }
            return null;
        }
    }
}
