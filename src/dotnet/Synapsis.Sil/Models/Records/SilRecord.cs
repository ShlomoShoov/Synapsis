using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil.Models.Records.Parts;

namespace Synapsis.Sil.Models.Records;

/// <summary>
/// The fields every SIL record carries, whatever its kind: who it is, where it comes from, how much to trust it,
/// and when it became known. The four kinds (entity, event, relation, conclusion) add their own fields.
/// </summary>
/// <remarks>
/// Values are kept exactly as the producer sent them. Times stay text and vocabulary codes stay strings: the rules
/// read and judge them later (a bad date, an unknown source), so that every problem gets the code of the rule that
/// finds it, in the fixed rule order.
/// </remarks>
public abstract class SilRecord
{
    /// <summary>Which of the four kinds this record is.</summary>
    public required RecordKind Kind { get; init; }

    /// <summary>
    /// The record's id: a prefix for its kind (ent, evt, rel, con), the namespace of its package and the source's own
    /// key, for example "evt:kev:CVE-2023-34362". Re-sending the same source row always gives the same id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>A type code registered in a schema package, for example "CYB.VULN.KNOWN_EXPLOITED" or "Bank".</summary>
    public required string Type { get; init; }

    /// <summary>The source that states this record, as declared in a schema package, for example "src:cisa:kev".</summary>
    public required string Source { get; init; }

    /// <summary>How much this record can be trusted, from 0 to 100.</summary>
    public required int Reliability { get; init; }

    /// <summary>Why it has that reliability: a NATO code from A1 to F6, or measured, analyst, default or model.</summary>
    public required string ReliabilityBasis { get; init; }

    /// <summary>
    /// When this record became known, as a UTC instant such as "2023-06-03T00:00:00Z". Every read "as of" a date
    /// returns only what was known by then.
    /// </summary>
    public required string KnownAt { get; init; }

    /// <summary>
    /// How KnownAt was established: published, observed, proxy_event_end, proxy_publication_lag, or ingested (set by
    /// the server when a change arrives with a time in the past).
    /// </summary>
    public required string KnownAtBasis { get; init; }

    /// <summary>Optional words about the record (a title, a body, a language); what search reads.</summary>
    public TextBlock? Text { get; init; }

    /// <summary>
    /// Optional fields of the source's own, each declared by the record's type in its package. Kept as raw JSON
    /// values, because each attribute has its own declared type (text, integer, true/false, date, list of text).
    /// </summary>
    public Dictionary<string, JsonElement>? Attributes { get; init; }

    /// <summary>Optional pointer to where the record can be checked: a link and a reference at the source.</summary>
    public Evidence? Evidence { get; init; }
}
