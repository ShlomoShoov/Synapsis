namespace Synapsis.Sil.Records.Parts;

/// <summary>
/// An entity that took part in an event, and in which role.
/// Example: { "entity": "ent:kev-vendor:progress", "role": "vendor" }.
/// </summary>
public sealed class Participant
{
    /// <summary>The entity's id; the entity must be stored already or sent in the same batch.</summary>
    public required string Entity { get; init; }

    /// <summary>The role, as allowed by the event's type, for example "subject", "actor" or "vendor".</summary>
    public required string Role { get; init; }
}
