namespace Synapsis.Data.Models.Records;

/// <summary>An entity that took part in an event, and its role.</summary>
public sealed class Participant
{
    /// <summary>The entity's id.</summary>
    public string? Entity { get; set; }

    /// <summary>Its role, such as "victim" or "subject".</summary>
    public string? Role { get; set; }
}
