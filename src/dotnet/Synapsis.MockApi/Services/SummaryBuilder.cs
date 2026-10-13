using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Entities;

namespace Synapsis.MockApi.Services;

/// <summary>
/// Builds the summary of one record from its statements (each source's latest state): the fields come from the most
/// reliable statement, sources lists every source, known_at is the earliest. A value the record does not give stays null.
/// </summary>
public static class SummaryBuilder
{
    /// <summary>The summary of one record, with the names of the entities it names (for participant labels).</summary>
    public static RecordSummary Build(List<SampleRecord> statements, Dictionary<string, string> entityNames)
    {
        // 1. The most reliable statement gives the fields; every source is listed; the earliest known_at.
        SampleRecord primary = MostReliable(statements);
        SilRecord record = primary.ReadRecord!;
        List<string> sources = new List<string>();
        DateTime firstKnown = primary.KnownAt;
        foreach (SampleRecord statement in statements)
        {
            sources.Add(statement.Source);
            firstKnown = statement.KnownAt < firstKnown ? statement.KnownAt : firstKnown;
        }
        sources.Sort(string.CompareOrdinal);

        // 2. An event's time, place, measures and participants.
        EventRecord? happening = record as EventRecord;
        Place? place = happening?.Place;
        string? areaName = place?.Admin?.Name ?? PlaceName(record);
        List<EntityLabel> participants = Participants(happening, entityNames);

        // 3. The label.
        string label = Labels.For(record, areaName, participants, entityNames, out string labelFrom);
        return new RecordSummary
        {
            Id = primary.Id, Kind = primary.Kind, Type = primary.Type, Sources = sources, KnownAt = firstKnown,
            Label = label, LabelFrom = labelFrom,
            TimeStart = happening?.Time?.Start?.UtcDateTime, TimeEnd = happening?.Time?.End?.UtcDateTime,
            TimePrecision = happening?.Time?.Precision,
            Point = place != null && place.Point.Count == 2 ? place.Point : null, UncertaintyKm = place?.UncertaintyKm,
            Country = place?.Admin?.Country, Subdivision = place?.Admin?.Subdivision, AreaName = areaName,
            Logical = LogicalNodes(place), Measures = happening?.Measures ?? new List<Measure>(), Participants = participants,
        };
    }

    /// <summary>The statement with the highest reliability; at equal reliability the earliest known.</summary>
    public static SampleRecord MostReliable(List<SampleRecord> statements)
    {
        SampleRecord best = statements[0];
        foreach (SampleRecord statement in statements)
        {
            int reliability = statement.ReadRecord?.Reliability ?? 0;
            int bestReliability = best.ReadRecord?.Reliability ?? 0;
            if (reliability > bestReliability || (reliability == bestReliability && statement.KnownAt < best.KnownAt))
            {
                best = statement;
            }
        }
        return best;
    }

    /// <summary>The place name some sources give as the attribute place_name (USGS, GDELT); null when absent.</summary>
    private static string? PlaceName(SilRecord record)
    {
        if (record.Attributes.TryGetValue("place_name", out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }

    /// <summary>The entities taking part, each once, with their names (the id when no name is known).</summary>
    private static List<EntityLabel> Participants(EventRecord? happening, Dictionary<string, string> entityNames)
    {
        List<EntityLabel> participants = new List<EntityLabel>();
        HashSet<string> seen = new HashSet<string>();
        if (happening == null)
        {
            return participants;
        }
        foreach (Participant participant in happening.Participants)
        {
            if (participant.Entity != null && seen.Add(participant.Entity))
            {
                string label = entityNames.ContainsKey(participant.Entity) ? entityNames[participant.Entity] : participant.Entity;
                participants.Add(new EntityLabel { Id = participant.Entity, Label = label });
            }
        }
        return participants;
    }

    /// <summary>The logical node the event is at, such as "cyber.vendor_product/Apache/Log4j2" (the longest prefix).</summary>
    private static List<string> LogicalNodes(Place? place)
    {
        List<string> nodes = new List<string>();
        List<string> prefixes = LatticeKeys.LogicalPrefixes(place?.Logical);
        if (prefixes.Count > 0)
        {
            nodes.Add(prefixes[prefixes.Count - 1]);
        }
        return nodes;
    }
}
