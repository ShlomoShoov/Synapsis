namespace Synapsis.Api.Models.Records;

/// <summary>An event: something that happened at a time, such as an earthquake or a bank failure.</summary>
public sealed class EventRecord : SilRecord
{
    /// <summary>When it happened (required, with a start).</summary>
    public EventTime? Time { get; set; }
}
