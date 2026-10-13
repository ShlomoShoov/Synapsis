using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.MockApi.Models.Dto;

namespace Synapsis.MockApi.Services;

/// <summary>Small helpers of ConnectionFinder: a connection from a summary, a group in its order, an entity's roles in an event.</summary>
public static class ConnectedRecords
{
    /// <summary>The summary as a connection, with how it is connected (each value only for its kind of group).</summary>
    public static ConnectedRecord Connect(RecordSummary summary, string? role, string? direction, int? sharedEvents)
    {
        return new ConnectedRecord
        {
            Id = summary.Id, Kind = summary.Kind, Type = summary.Type, Sources = summary.Sources, KnownAt = summary.KnownAt,
            Label = summary.Label, LabelFrom = summary.LabelFrom, TimeStart = summary.TimeStart, TimeEnd = summary.TimeEnd,
            TimePrecision = summary.TimePrecision, Point = summary.Point, UncertaintyKm = summary.UncertaintyKm,
            Country = summary.Country, Subdivision = summary.Subdivision, AreaName = summary.AreaName, Logical = summary.Logical,
            Measures = summary.Measures, Participants = summary.Participants,
            Role = role, Direction = direction, SharedEvents = sharedEvents,
        };
    }

    /// <summary>
    /// A group with all its connections in order: events newest first, shared entities by most shared events,
    /// the others by label.
    /// </summary>
    public static ConnectionGroup Group(string key, string kind, string? type, string label, List<ConnectedRecord> connections)
    {
        if (kind == "event")
        {
            connections.Sort(CompareNewestFirst);
        }
        else if (kind == "together")
        {
            connections.Sort(CompareMostShared);
        }
        else
        {
            connections.Sort(CompareByLabel);
        }
        return new ConnectionGroup { Key = key, Kind = kind, Type = type, Label = label, Count = connections.Count, Items = connections };
    }

    /// <summary>The entity's roles in the event, each once, joined by ", " (a git author can also be the committer).</summary>
    public static string RolesOf(EventRecord happening, string entityId)
    {
        List<string> roles = new List<string>();
        foreach (Participant participant in happening.Participants)
        {
            if (participant.Entity == entityId && participant.Role != null && !roles.Contains(participant.Role))
            {
                roles.Add(participant.Role);
            }
        }
        return string.Join(", ", roles);
    }

    /// <summary>Newest start first; then by id.</summary>
    private static int CompareNewestFirst(ConnectedRecord first, ConnectedRecord second)
    {
        int byTime = System.Nullable.Compare(second.TimeStart, first.TimeStart);
        return byTime != 0 ? byTime : string.CompareOrdinal(first.Id, second.Id);
    }

    /// <summary>Most shared events first; then by label.</summary>
    private static int CompareMostShared(ConnectedRecord first, ConnectedRecord second)
    {
        int byShared = (second.SharedEvents ?? 0).CompareTo(first.SharedEvents ?? 0);
        return byShared != 0 ? byShared : CompareByLabel(first, second);
    }

    /// <summary>By label, then by id.</summary>
    private static int CompareByLabel(ConnectedRecord first, ConnectedRecord second)
    {
        int byLabel = string.CompareOrdinal(first.Label, second.Label);
        return byLabel != 0 ? byLabel : string.CompareOrdinal(first.Id, second.Id);
    }
}
