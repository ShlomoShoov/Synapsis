using System.Collections.Generic;
using Synapsis.Sil.Records.Parts;

namespace Synapsis.Sil.Records;

/// <summary>
/// An event: something that happened at a time and a place, with the entities that took part and the values that
/// were measured. Example: CISA adds CVE-2023-34362 to its catalog on 2 June 2023 (place: vendor / product).
/// </summary>
public sealed class EventRecord : SilRecord
{
    /// <summary>When it happened (valid time), with the precision the source really knows.</summary>
    public required ValidTime Time { get; init; }

    /// <summary>Where it happened: a point, an administrative area, a logical position, or explicitly nowhere.</summary>
    public required Place Place { get; init; }

    /// <summary>The entities that took part, each in a role (for example the vulnerability as "subject").</summary>
    public List<Participant>? Participants { get; init; }

    /// <summary>The values measured, each with its name and unit (for example deaths, magnitude, total assets).</summary>
    public List<Measure>? Measures { get; init; }
}
