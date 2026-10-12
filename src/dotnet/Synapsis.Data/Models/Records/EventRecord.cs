using System.Collections.Generic;

namespace Synapsis.Data.Models.Records;

/// <summary>An event: something that happened at a time and a place, such as an earthquake or a bank failure.</summary>
public sealed class EventRecord : SilRecord
{
    /// <summary>When it happened.</summary>
    public ValidTime? Time { get; set; }

    /// <summary>Where it happened.</summary>
    public Place? Place { get; set; }

    /// <summary>The entities that took part, each in a role.</summary>
    public List<Participant> Participants { get; set; } = new List<Participant>();

    /// <summary>The numbers the source gives about it.</summary>
    public List<Measure> Measures { get; set; } = new List<Measure>();
}
