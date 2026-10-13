using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;

namespace Synapsis.Elasticsearch;

/// <summary>
/// Turns one stored record (its MongoDB envelope and the record read from it) into a search document. Pure code: no
/// store and no network; the vector is added afterwards by the orchestrator. A value the record does not give stays
/// null, a list stays empty.
/// </summary>
public static class SearchDocumentMapper
{
    /// <summary>How a conclusion's about names a map cell: "ent:h3:&lt;cell&gt;" (the engine's contract) or "subject:h3:&lt;cell&gt;" (SIL's subject key).</summary>
    private static readonly string[] CellPrefixes = { "ent:h3:", "subject:h3:" };

    /// <summary>The id after its prefix and namespace ("evt:kev:CVE-2023-4966" → "CVE-2023-4966"); the whole id when it has fewer than two colons.</summary>
    public static string IdKeyOf(string id)
    {
        int firstColon = id.IndexOf(':');
        int secondColon = firstColon < 0 ? -1 : id.IndexOf(':', firstColon + 1);
        return secondColon < 0 ? id : id.Substring(secondColon + 1);
    }

    /// <summary>The search document: the identity from the envelope, then what the record's kind adds.</summary>
    public static SearchDocument Map(StoredRecord stored, SilRecord record)
    {
        // 1. Identity, from the envelope.
        SearchDocument document = new SearchDocument
        {
            Id = stored.RecordId,
            IdKey = IdKeyOf(stored.RecordId),
            RecordKey = StoredRecord.KeyOf(stored.RecordId, stored.Source),
            State = stored.State,
            Kind = stored.Kind,
            Type = stored.Type,
            Source = stored.Source,
            KnownAt = stored.KnownAt
        };

        // 2. What each kind adds: an entity its name; an event its text, time, place, measures and entities; a conclusion
        //    its text, what it is about (entities, and a map cell), the period it speaks of, its numbers and its
        //    logical place, so the read routes (map, entity, date) show it like an event.
        switch (record)
        {
            case EntityRecord entity:
                document.Name = entity.Name;
                break;
            case EventRecord eventRecord:
                AddText(document, eventRecord.Text);
                AddTime(document, eventRecord.Time);
                AddPlace(document, eventRecord.Place);
                AddMeasures(document, eventRecord.Measures);
                AddEntities(document, eventRecord.Participants);
                break;
            case ConclusionRecord conclusion:
                AddText(document, conclusion.Text);
                AddAbout(document, conclusion.About);
                AddPeriod(document, conclusion.Attributes);
                AddStatementMeasures(document, conclusion.Statement, conclusion.Confidence);
                AddLogical(document, conclusion.Attributes);
                break;
        }
        return document;
    }

    /// <summary>The text to turn into a vector: the title and the body, joined by a new line; null when there is neither.</summary>
    public static string? TextToEmbed(SearchDocument document)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            parts.Add(document.Title);
        }
        if (!string.IsNullOrWhiteSpace(document.Body))
        {
            parts.Add(document.Body);
        }
        if (parts.Count == 0)
        {
            return null;
        }
        return string.Join("\n", parts);
    }

    /// <summary>The title and body of an event or conclusion.</summary>
    private static void AddText(SearchDocument document, TextBlock? text)
    {
        if (text == null)
        {
            return;
        }
        document.Title = text.Title;
        document.Body = text.Body;
    }

    /// <summary>The start (in UTC), its precision, and the Monday of its week.</summary>
    private static void AddTime(SearchDocument document, ValidTime? time)
    {
        if (time == null)
        {
            return;
        }
        document.TimeStart = time.Start?.UtcDateTime;
        document.TimePrecision = time.Precision;
        document.Week = LatticeKeys.Week(document.TimeStart, time.Precision);
    }

    /// <summary>The point and its H3 cells, the country and subdivision, and the prefixes of the logical path.</summary>
    private static void AddPlace(SearchDocument document, Place? place)
    {
        if (place == null)
        {
            return;
        }
        if (place.Point.Count > 0)
        {
            document.Location = place.Point;
            document.H3R2 = LatticeKeys.Cell(place.Point, place.UncertaintyKm, 2);
            document.H3R3 = LatticeKeys.Cell(place.Point, place.UncertaintyKm, 3);
            document.H3R4 = LatticeKeys.Cell(place.Point, place.UncertaintyKm, 4);
            document.H3R5 = LatticeKeys.Cell(place.Point, place.UncertaintyKm, 5);
        }
        if (place.Admin != null)
        {
            document.Country = place.Admin.Country;
            document.Subdivision = place.Admin.Subdivision;
        }
        document.LogicalPrefixes = LatticeKeys.LogicalPrefixes(place.Logical);
    }

    /// <summary>Each measure's value under its name; a name given twice keeps both values; a measure without a value is left out.</summary>
    private static void AddMeasures(SearchDocument document, List<Measure> measures)
    {
        foreach (Measure measure in measures)
        {
            if (measure.Name == null || measure.Value == null)
            {
                continue;
            }
            if (!document.Measures.ContainsKey(measure.Name))
            {
                document.Measures[measure.Name] = new List<double>();
            }
            document.Measures[measure.Name].Add(measure.Value.Value);
        }
    }

    /// <summary>The ids of the entities that took part, each once, in the order they first appear.</summary>
    private static void AddEntities(SearchDocument document, List<Participant> participants)
    {
        foreach (Participant participant in participants)
        {
            if (participant.Entity != null && !document.Entities.Contains(participant.Entity))
            {
                document.Entities.Add(participant.Entity);
            }
        }
    }

    /// <summary>
    /// What a conclusion is about: every id in about, each once, under entities (so /search?entity= lists it); the
    /// first one that names a valid H3 cell ("ent:h3:&lt;cell&gt;" or "subject:h3:&lt;cell&gt;") places it on the map: the
    /// location is the cell's centre, and h3_r2..h3_r5 are the cell and its parents (none finer than the cell).
    /// </summary>
    private static void AddAbout(SearchDocument document, List<string> about)
    {
        foreach (string target in about)
        {
            // 1. Every id it is about, each once.
            if (!document.Entities.Contains(target))
            {
                document.Entities.Add(target);
            }

            // 2. The first one that names a valid H3 cell places it; the ones after it are only entities.
            foreach (string prefix in CellPrefixes)
            {
                if (document.Location != null || !target.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                (List<double> Centre, Dictionary<int, string> Cells)? cell = LatticeKeys.CellCentreAndParents(target.Substring(prefix.Length));
                if (cell == null)
                {
                    continue;
                }
                document.Location = cell.Value.Centre;
                document.H3R2 = cell.Value.Cells.GetValueOrDefault(2);
                document.H3R3 = cell.Value.Cells.GetValueOrDefault(3);
                document.H3R4 = cell.Value.Cells.GetValueOrDefault(4);
                document.H3R5 = cell.Value.Cells.GetValueOrDefault(5);
            }
        }
    }

    /// <summary>
    /// The period a conclusion speaks of: time_start from attributes.period_start (ISO text, read as the record reader
    /// reads times: without an offset it is UTC), else known_at; the precision from attributes.period_precision, else
    /// "day"; and the Monday of its week.
    /// </summary>
    private static void AddPeriod(SearchDocument document, Dictionary<string, JsonElement> attributes)
    {
        // 1. The start: period_start when it is an ISO time, else known_at.
        document.TimeStart = document.KnownAt;
        if (attributes.TryGetValue("period_start", out JsonElement start) && start.ValueKind == JsonValueKind.String && start.TryGetDateTime(out DateTime time))
        {
            if (time.Kind == DateTimeKind.Unspecified)
            {
                time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
            }
            document.TimeStart = time.ToUniversalTime();
        }

        // 2. The precision: period_precision when it is text, else "day"; then the week (only for a week or finer).
        document.TimePrecision = "day";
        if (attributes.TryGetValue("period_precision", out JsonElement precision) && precision.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(precision.GetString()))
        {
            document.TimePrecision = precision.GetString();
        }
        document.Week = LatticeKeys.Week(document.TimeStart, document.TimePrecision);
    }

    /// <summary>
    /// A conclusion's numbers as measures: each numeric field of statement.value under its name (a bare number under
    /// "value"), and the confidence under "confidence". A field that is not a number is left out.
    /// </summary>
    private static void AddStatementMeasures(SearchDocument document, Statement? statement, double? confidence)
    {
        List<Measure> measures = new List<Measure>();
        JsonElement? value = statement?.Value;
        if (value != null && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out double bare))
        {
            measures.Add(new Measure { Name = "value", Value = bare });
        }
        if (value != null && value.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty field in value.Value.EnumerateObject())
            {
                if (field.Value.ValueKind == JsonValueKind.Number && field.Value.TryGetDouble(out double number))
                {
                    measures.Add(new Measure { Name = field.Name, Value = number });
                }
            }
        }
        measures.Add(new Measure { Name = "confidence", Value = confidence });   // left out by AddMeasures when there is none
        AddMeasures(document, measures);
    }

    /// <summary>
    /// The prefixes of the logical place a conclusion speaks of, attributes.logical = {"space": ..., "path": [...]},
    /// when it is given. The path ends at its first step that is not text, so every prefix is a true parent.
    /// </summary>
    private static void AddLogical(SearchDocument document, Dictionary<string, JsonElement> attributes)
    {
        if (!attributes.TryGetValue("logical", out JsonElement logical) || logical.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        LogicalPlace place = new LogicalPlace();
        if (logical.TryGetProperty("space", out JsonElement space) && space.ValueKind == JsonValueKind.String)
        {
            place.Space = space.GetString();
        }
        if (logical.TryGetProperty("path", out JsonElement path) && path.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement step in path.EnumerateArray())
            {
                if (step.ValueKind != JsonValueKind.String)
                {
                    break;
                }
                place.Path.Add(step.GetString()!);
            }
        }
        document.LogicalPrefixes = LatticeKeys.LogicalPrefixes(place);
    }
}
