namespace Synapsis.MockApi.Models.Dto;

/// <summary>One connection of a point: the connected record, plus how it is connected (which field is set depends on the group's kind).</summary>
public sealed class ConnectedRecord : RecordSummary
{
    /// <summary>event and participant groups: the entity's role in the event, such as "actor" or "side_a".</summary>
    public string? Role { get; init; }

    /// <summary>relation groups: "out" when the relation goes from the point to this record, "in" when it comes to the point.</summary>
    public string? Direction { get; init; }

    /// <summary>together groups: how many events the point and this entity both take part in.</summary>
    public int? SharedEvents { get; init; }
}
