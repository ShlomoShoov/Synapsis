using System.Collections.Generic;
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

        // 2. What each kind adds: an entity its name; an event its text, time, place, measures and entities; a conclusion its text.
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
}
