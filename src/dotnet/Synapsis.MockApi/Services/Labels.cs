using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Synapsis.Data.Models.Records;
using Synapsis.MockApi.Models.Dto;

namespace Synapsis.MockApi.Services;

/// <summary>
/// What to show for a record (docs/phase-2-read-api.md §5.3): its name, title, the start of its body, a conclusion's
/// statement, or, for an event without text, "type · where · what". Derived for display, never stored.
/// </summary>
public static class Labels
{
    /// <summary>How much of a body a label keeps.</summary>
    private const int BodyLength = 140;

    /// <summary>The label of a record, and in labelFrom where it came from (name, title, body, statement, generated).</summary>
    public static string For(SilRecord record, string? areaName, List<EntityLabel> participants, Dictionary<string, string> entityNames, out string labelFrom)
    {
        if (record is EntityRecord entity)
        {
            labelFrom = "name";
            return entity.Name ?? record.Id ?? string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(record.Text?.Title))
        {
            labelFrom = "title";
            return record.Text.Title.Trim();
        }
        if (!string.IsNullOrWhiteSpace(record.Text?.Body))
        {
            labelFrom = "body";
            return StartOf(record.Text.Body);
        }
        if (record is ConclusionRecord conclusion)
        {
            labelFrom = "statement";
            return Statement(conclusion, entityNames);
        }
        labelFrom = "generated";
        return Generated(record, areaName, participants);
    }

    /// <summary>The first 140 characters of a text, cut at a word, with "…" when cut.</summary>
    private static string StartOf(string body)
    {
        string text = Regex.Replace(body, "\\s+", " ").Trim();
        if (text.Length <= BodyLength)
        {
            return text;
        }
        string start = text.Substring(0, BodyLength);
        int lastSpace = start.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            start = start.Substring(0, lastSpace);
        }
        return start.TrimEnd(',', ';', ':') + "…";
    }

    /// <summary>"subjects: predicate object", such as "Log4j2: in_logical_space cyber.product_class/LIBRARY".</summary>
    private static string Statement(ConclusionRecord conclusion, Dictionary<string, string> entityNames)
    {
        List<string> subjects = new List<string>();
        foreach (string about in conclusion.About)
        {
            subjects.Add(entityNames.ContainsKey(about) ? entityNames[about] : about);
        }
        string what = conclusion.Statement?.Object ?? conclusion.Statement?.Value?.ToString() ?? string.Empty;
        return string.Join(", ", subjects) + ": " + conclusion.Statement?.Predicate + " " + what;
    }

    /// <summary>"type · where · what" for an event without text; a part the record does not give is left out.</summary>
    private static string Generated(SilRecord record, string? areaName, List<EntityLabel> participants)
    {
        List<string> parts = new List<string> { record.Type ?? string.Empty };
        EventRecord? happening = record as EventRecord;
        string? where = areaName;
        if (where == null && happening?.Place?.Logical != null)
        {
            where = string.Join(" / ", happening.Place.Logical.Path);
        }
        where = where ?? happening?.Place?.Admin?.Subdivision ?? happening?.Place?.Admin?.Country;
        if (!string.IsNullOrEmpty(where))
        {
            parts.Add(where);
        }
        string? what = What(happening, participants);
        if (what != null)
        {
            parts.Add(what);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>Who took part ("A – B"), else the first measure ("magnitude 5.99"; the unit is left out when it repeats the name).</summary>
    private static string? What(EventRecord? happening, List<EntityLabel> participants)
    {
        if (participants.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (EntityLabel participant in participants)
            {
                names.Add(participant.Label);
            }
            return string.Join(" – ", names);
        }
        if (happening == null || happening.Measures.Count == 0 || happening.Measures[0].Value == null)
        {
            return null;
        }
        Measure first = happening.Measures[0];
        string value = first.Value!.Value.ToString(CultureInfo.InvariantCulture);
        bool unitRepeatsName = first.Unit == null || first.Unit == first.Name;
        return first.Name + " " + value + (unitRepeatsName ? string.Empty : " " + first.Unit);
    }
}
