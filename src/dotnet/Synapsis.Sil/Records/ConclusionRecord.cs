using System.Collections.Generic;
using Synapsis.Sil.Records.Parts;

namespace Synapsis.Sil.Records;

/// <summary>
/// A conclusion: a claim derived from other records, never a fact of a source. It says what it is about, what it
/// claims, which records it rests on, who or what produced it, and how sure it is. Model output and every engine
/// finding are conclusions, so they can always be traced back to their premises.
/// </summary>
public sealed class ConclusionRecord : SilRecord
{
    /// <summary>
    /// What the claim is about: record ids, or subject keys for things that are not records, such as a map cell
    /// ("subject:h3:..."), a logical node ("subject:logical:...") or an area ("subject:admin:US-CA").
    /// </summary>
    public required List<string> About { get; init; }

    /// <summary>The claim itself: a predicate and either a target (Object) or a value.</summary>
    public required Statement Statement { get; init; }

    /// <summary>The ids of the records the claim rests on.</summary>
    public required List<string> Premises { get; init; }

    /// <summary>Who or what produced the claim (a human, an agent, the system) and with which method and version.</summary>
    public required Method Method { get; init; }

    /// <summary>How sure the producer is, from 0 to 100.</summary>
    public required double Confidence { get; init; }
}
