namespace Synapsis.Sil.Models.Domain.Records;

/// <summary>
/// The four kinds of SIL record. The kind decides which fields a record has and which MongoDB collection stores it.
/// In JSON it is written in lower case: "entity", "event", "relation", "conclusion".
/// </summary>
public enum RecordKind
{
    /// <summary>A thing that persists and is referred to: a bank, a vulnerability, a vessel.</summary>
    Entity,

    /// <summary>Something that happened at a time and a place: a vulnerability added to a catalog, an earthquake.</summary>
    Event,

    /// <summary>A lasting link between two entities: a product belongs to a vendor.</summary>
    Relation,

    /// <summary>A claim derived from other records, with its premises and the method that made it.</summary>
    Conclusion,
}
