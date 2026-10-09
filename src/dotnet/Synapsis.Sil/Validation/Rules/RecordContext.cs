using System;
using Synapsis.Sil.Records;
using Synapsis.Sil.Records.Parts;
using Synapsis.Sil.Registry;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// What every record rule may read: the typed record, the registry to look things up in, and the moment the server
/// received the record. Nothing in it changes while the rules run, so the rules cannot affect each other.
/// </summary>
public sealed class RecordContext
{
    /// <summary>Creates the context of one record.</summary>
    public RecordContext(SilRecord record, RegistrySnapshot registry, DateTime receivedAt)
    {
        Record = record;
        Registry = registry;
        ReceivedAt = receivedAt;
    }

    /// <summary>The record being checked.</summary>
    public SilRecord Record { get; }

    /// <summary>The vocabulary to check it against.</summary>
    public RegistrySnapshot Registry { get; }

    /// <summary>When the server received the record (UTC): the limit for known_at.</summary>
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
