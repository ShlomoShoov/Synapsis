using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Synapsis.Data.Models.Records;

/// <summary>
/// What every SIL 1.2 record has, whatever its kind (format: schemas/sil/). Fields only. A single value the JSON does
/// not give stays null; a list or a dictionary it does not give is empty. Nothing is ever invented.
/// </summary>
public abstract class SilRecord
{
    /// <summary>entity, event, relation or conclusion.</summary>
    public string? Kind { get; set; }

    /// <summary>The record's id, such as "evt:kev:CVE-2021-44228".</summary>
    public string? Id { get; set; }

    /// <summary>The record's type, such as "CYB.VULN.KNOWN_EXPLOITED".</summary>
    public string? Type { get; set; }

    /// <summary>The source that states it, such as "src:cisa:kev".</summary>
    public string? Source { get; set; }

    /// <summary>How reliable the source is for this record, 0 to 100.</summary>
    public int? Reliability { get; set; }

    /// <summary>Why: a NATO grade (A1...F6) or measured, analyst, default, model.</summary>
    public string? ReliabilityBasis { get; set; }

    /// <summary>When the source says this was known.</summary>
    public DateTimeOffset? KnownAt { get; set; }

    /// <summary>How known_at was chosen: published, observed, proxy_event_end, proxy_publication_lag, ingested.</summary>
    public string? KnownAtBasis { get; set; }

    /// <summary>A title and body, when the record has text.</summary>
    public TextBlock? Text { get; set; }

    /// <summary>Extra values the source gives, by name, kept as JSON (their shape differs by type).</summary>
    public Dictionary<string, JsonElement> Attributes { get; set; } = new Dictionary<string, JsonElement>();

    /// <summary>Where the record can be checked.</summary>
    public Evidence? Evidence { get; set; }
}
