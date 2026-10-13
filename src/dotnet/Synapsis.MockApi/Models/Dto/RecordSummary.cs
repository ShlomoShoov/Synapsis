using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;

namespace Synapsis.MockApi.Models.Dto;

/// <summary>
/// One record as the map and the connections show it: enough for a popup (label, type, time, place, participants).
/// When several sources state the same id it is one summary: the fields come from the most reliable statement, sources
/// lists them all, known_at is the earliest. A value the record does not give stays null (a list stays empty). It is
/// not sealed: a connection is a summary plus how it is connected (ConnectedRecord).
/// </summary>
public class RecordSummary
{
    /// <summary>The record's id.</summary>
    public required string Id { get; init; }

    /// <summary>entity, event or conclusion.</summary>
    public required string Kind { get; init; }

    /// <summary>The record's type, such as "SEC.ATTACK.BOMBING".</summary>
    public required string Type { get; init; }

    /// <summary>Every source that states this id (known at as_of).</summary>
    public List<string> Sources { get; init; } = new List<string>();

    /// <summary>When it first became known (the earliest statement), in UTC.</summary>
    public DateTime KnownAt { get; init; }

    /// <summary>What to show: derived for display, never stored (see LabelFrom).</summary>
    public required string Label { get; init; }

    /// <summary>Where the label came from: name, title, body (its first 140 characters), statement or generated ("type · where · what").</summary>
    public required string LabelFrom { get; init; }

    /// <summary>When the event started, in UTC.</summary>
    public DateTime? TimeStart { get; init; }

    /// <summary>When the event ended, in UTC (most events give no end).</summary>
    public DateTime? TimeEnd { get; init; }

    /// <summary>How precise the time is: second, minute, hour, day, week, month, quarter or year.</summary>
    public string? TimePrecision { get; init; }

    /// <summary>The event's point, [longitude, latitude].</summary>
    public List<double>? Point { get; init; }

    /// <summary>How far the real place may be from the point, in km.</summary>
    public double? UncertaintyKm { get; init; }

    /// <summary>The country code (ISO 3166-1).</summary>
    public string? Country { get; init; }

    /// <summary>The subdivision code (ISO 3166-2).</summary>
    public string? Subdivision { get; init; }

    /// <summary>The place name the source gives (the admin name, or the attribute place_name).</summary>
    public string? AreaName { get; init; }

    /// <summary>The logical nodes it is at, such as "cyber.vendor_product/Apache/Log4j2".</summary>
    public List<string> Logical { get; init; } = new List<string>();

    /// <summary>The numbers the event gives, with their units.</summary>
    public List<Measure> Measures { get; init; } = new List<Measure>();

    /// <summary>The entities that take part (an event), with their labels; the roles are in the full record.</summary>
    public List<EntityLabel> Participants { get; init; } = new List<EntityLabel>();
}
