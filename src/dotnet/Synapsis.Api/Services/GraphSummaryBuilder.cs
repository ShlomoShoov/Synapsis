using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Synapsis.Api.Models.Dto;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;

namespace Synapsis.Api.Services;

/// <summary>Builds the flat display summary from the source states selected from MongoDB.</summary>
public static class GraphSummaryBuilder
{
    private const int BodyLength = 140;

    /// <summary>Lists the entities named by a conclusion so MongoDB can resolve their display names.</summary>
    internal static List<string> AboutEntityIds(IReadOnlyList<StoredRecord> statements)
    {
        List<string> ids = new List<string>();
        if (MostReliable(statements).Record is ConclusionRecord conclusion)
        {
            foreach (string about in conclusion.About) ids.Add(about);
        }
        return ids;
    }

    /// <summary>Creates the display fields from the most reliable statement and includes every selected source.</summary>
    public static RecordSummary Build(IReadOnlyList<StoredRecord> statements, IReadOnlyDictionary<string, string> entityNames)
    {
        (StoredRecord Stored, SilRecord Record) primary = MostReliable(statements);
        EventRecord? eventRecord = primary.Record as EventRecord;
        Place? place = eventRecord?.Place;
        string? areaName = place?.Admin?.Name ?? PlaceName(primary.Record);
        List<EntityLabel> participants = Participants(eventRecord, entityNames);
        string label = Label(primary.Record, areaName, participants, entityNames, out string labelFrom);
        List<string> sources = Sources(statements);
        DateTime knownAt = EarliestKnownAt(statements);
        return new RecordSummary
        {
            Id = primary.Stored.RecordId,
            Kind = primary.Stored.Kind,
            Type = primary.Stored.Type,
            Sources = sources,
            KnownAt = knownAt,
            Label = label,
            LabelFrom = labelFrom,
            TimeStart = eventRecord?.Time?.Start?.UtcDateTime,
            TimeEnd = eventRecord?.Time?.End?.UtcDateTime,
            TimePrecision = eventRecord?.Time?.Precision,
            Point = place != null && place.Point.Count == 2 ? place.Point : null,
            UncertaintyKm = place?.UncertaintyKm,
            Country = place?.Admin?.Country,
            Subdivision = place?.Admin?.Subdivision,
            AreaName = areaName,
            Logical = LogicalNodes(place),
            Measures = eventRecord?.Measures ?? new List<Measure>(),
            Participants = participants
        };
    }

    /// <summary>Chooses the most reliable statement, then earliest known time, then ordinal source id.</summary>
    public static (StoredRecord Stored, SilRecord Record) MostReliable(IReadOnlyList<StoredRecord> statements)
    {
        if (statements.Count == 0)
        {
            throw new InvalidOperationException("A graph summary needs at least one readable stored record.");
        }
        StoredRecord bestStored = statements[0];
        SilRecord bestRecord = Read(bestStored);
        foreach (StoredRecord statement in statements)
        {
            SilRecord record = Read(statement);
            int reliability = record.Reliability ?? 0;
            int bestReliability = bestRecord.Reliability ?? 0;
            if (IsMoreReliable(statement, bestStored, reliability, bestReliability))
            {
                bestStored = statement;
                bestRecord = record;
            }
        }
        return (bestStored, bestRecord);
    }

    /// <summary>Checks reliability and deterministic tie breakers for two source statements.</summary>
    private static bool IsMoreReliable(StoredRecord candidate, StoredRecord current, int candidateReliability, int currentReliability)
    {
        if (candidateReliability != currentReliability) return candidateReliability > currentReliability;
        if (candidate.KnownAt != current.KnownAt) return candidate.KnownAt < current.KnownAt;
        int sourceOrder = string.CompareOrdinal(candidate.Source, current.Source);
        if (sourceOrder != 0) return sourceOrder < 0;
        return string.CompareOrdinal(candidate.Key, current.Key) < 0;
    }

    /// <summary>Reads one stored BSON record with the shared record reader or reports its data error.</summary>
    private static SilRecord Read(StoredRecord stored)
    {
        (SilRecord? record, string? error) = RecordReader.Read(RecordReader.ToJson(stored.Record));
        if (record == null)
        {
            throw new InvalidOperationException("Stored record '" + stored.RecordId + "' cannot be read: " + error);
        }
        return record;
    }

    /// <summary>Collects the source names in ordinal order.</summary>
    private static List<string> Sources(IReadOnlyList<StoredRecord> statements)
    {
        List<string> sources = new List<string>();
        foreach (StoredRecord statement in statements) sources.Add(statement.Source);
        sources.Sort(string.CompareOrdinal);
        return sources;
    }

    /// <summary>Returns the earliest time among all selected source statements.</summary>
    private static DateTime EarliestKnownAt(IReadOnlyList<StoredRecord> statements)
    {
        DateTime earliest = statements[0].KnownAt;
        foreach (StoredRecord statement in statements)
        {
            if (statement.KnownAt < earliest) earliest = statement.KnownAt;
        }
        return earliest;
    }

    /// <summary>Resolves distinct event participants to known entity labels or their ids.</summary>
    private static List<EntityLabel> Participants(EventRecord? eventRecord, IReadOnlyDictionary<string, string> entityNames)
    {
        List<EntityLabel> participants = new List<EntityLabel>();
        HashSet<string> seen = new HashSet<string>();
        if (eventRecord == null) return participants;
        foreach (Participant participant in eventRecord.Participants)
        {
            if (participant.Entity == null || !seen.Add(participant.Entity)) continue;
            string label = entityNames.TryGetValue(participant.Entity, out string? name) ? name : participant.Entity;
            participants.Add(new EntityLabel { Id = participant.Entity, Label = label });
        }
        return participants;
    }

    /// <summary>Chooses the same name, text, statement, or generated label order as the mock API.</summary>
    private static string Label(SilRecord record, string? areaName, List<EntityLabel> participants, IReadOnlyDictionary<string, string> entityNames, out string labelFrom)
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
            return BodyLabel(record.Text.Body);
        }
        if (record is ConclusionRecord conclusion)
        {
            labelFrom = "statement";
            return StatementLabel(conclusion, entityNames);
        }
        labelFrom = "generated";
        return GeneratedLabel(record, areaName, participants);
    }

    /// <summary>Shortens and cleans a body label at a word boundary.</summary>
    private static string BodyLabel(string body)
    {
        string text = Regex.Replace(body, "\\s+", " ").Trim();
        if (text.Length <= BodyLength) return text;
        string start = text.Substring(0, BodyLength);
        int lastSpace = start.LastIndexOf(' ');
        if (lastSpace > 0) start = start.Substring(0, lastSpace);
        return start.TrimEnd(',', ';', ':') + "…";
    }

    /// <summary>Builds the display text for a conclusion from its subjects, predicate, and object.</summary>
    private static string StatementLabel(ConclusionRecord conclusion, IReadOnlyDictionary<string, string> entityNames)
    {
        List<string> subjects = new List<string>();
        foreach (string about in conclusion.About)
        {
            subjects.Add(entityNames.TryGetValue(about, out string? name) ? name : about);
        }
        string what = conclusion.Statement?.Object ?? conclusion.Statement?.Value?.ToString() ?? string.Empty;
        return string.Join(", ", subjects) + ": " + conclusion.Statement?.Predicate + " " + what;
    }

    /// <summary>Builds the generated event label from its type, place, participants, or first measure.</summary>
    private static string GeneratedLabel(SilRecord record, string? areaName, List<EntityLabel> participants)
    {
        List<string> parts = new List<string> { record.Type ?? string.Empty };
        EventRecord? eventRecord = record as EventRecord;
        string? where = areaName ?? LogicalPath(eventRecord?.Place?.Logical);
        where = where ?? eventRecord?.Place?.Admin?.Subdivision ?? eventRecord?.Place?.Admin?.Country;
        if (!string.IsNullOrEmpty(where)) parts.Add(where);
        string? what = ParticipantsOrMeasure(eventRecord, participants);
        if (what != null) parts.Add(what);
        return string.Join(" · ", parts);
    }

    /// <summary>Uses event participants for the generated label, or the first measure when there are none.</summary>
    private static string? ParticipantsOrMeasure(EventRecord? eventRecord, List<EntityLabel> participants)
    {
        if (participants.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (EntityLabel participant in participants) names.Add(participant.Label);
            return string.Join(" – ", names);
        }
        if (eventRecord == null || eventRecord.Measures.Count == 0 || eventRecord.Measures[0].Value is not double measureValue) return null;
        Measure measure = eventRecord.Measures[0];
        string value = measureValue.ToString(CultureInfo.InvariantCulture);
        bool repeatsName = measure.Unit == null || measure.Unit == measure.Name;
        return measure.Name + " " + value + (repeatsName ? string.Empty : " " + measure.Unit);
    }

    /// <summary>Returns the most specific logical path text when the event has one.</summary>
    private static string? LogicalPath(LogicalPlace? logical)
    {
        if (logical == null) return null;
        return string.Join(" / ", logical.Path);
    }

    /// <summary>Returns the longest logical node prefix, when the event has a logical place.</summary>
    private static List<string> LogicalNodes(Place? place)
    {
        List<string> nodes = LatticeKeys.LogicalPrefixes(place?.Logical);
        if (nodes.Count <= 1) return nodes;
        return new List<string> { nodes[nodes.Count - 1] };
    }

    /// <summary>Reads an event place name stored in the free-form attributes.</summary>
    private static string? PlaceName(SilRecord record)
    {
        if (record.Attributes.TryGetValue("place_name", out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }
}
