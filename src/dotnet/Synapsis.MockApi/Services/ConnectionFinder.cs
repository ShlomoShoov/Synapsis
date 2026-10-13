using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.MockApi.Models.Dto;
using Synapsis.MockApi.Models.Entities;
using Synapsis.MockApi.Models.Requests;
using Synapsis.MockApi.Models.Results;

namespace Synapsis.MockApi.Services;

/// <summary>
/// The connections of a record, grouped by kind and type, every item of each group (paging comes after). An entity:
/// its relations, the entities that take part in the same events, the events it takes part in. An event: the entities
/// taking part. The filters keep the events through which things connect; relations follow as_of only.
/// </summary>
public static class ConnectionFinder
{
    /// <summary>The groups of the centre, in the order relation, together, event, participant.</summary>
    public static List<ConnectionGroup> Find(SampleItem center, Dictionary<string, SampleItem> items, List<SampleRecord> latest, AnalyzeFilters filters)
    {
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        if (center.Summary.Kind == "entity")
        {
            List<SampleItem> events = EventsOf(center.Summary.Id, items, filters);
            groups.AddRange(Relations(center.Summary.Id, items, latest));
            groups.AddRange(Together(center.Summary.Id, events, items));
            groups.AddRange(Events(center.Summary.Id, events));
        }
        if (center.Summary.Kind == "event")
        {
            groups.AddRange(Participants((EventRecord)center.Record, items));
        }
        return groups;
    }

    /// <summary>The relations known at as_of that start or end at the entity, by type and direction.</summary>
    private static List<ConnectionGroup> Relations(string entityId, Dictionary<string, SampleItem> items, List<SampleRecord> latest)
    {
        SortedDictionary<string, List<ConnectedRecord>> byKey = new SortedDictionary<string, List<ConnectedRecord>>();
        foreach (SampleRecord statement in latest)
        {
            if (statement.ReadRecord is not RelationRecord relation)
            {
                continue;
            }
            string direction = relation.From == entityId ? "out" : "in";
            string? other = relation.From == entityId ? relation.To : relation.From;
            if ((relation.From != entityId && relation.To != entityId) || other == null || !items.ContainsKey(other))
            {
                continue;
            }
            Add(byKey, statement.Type + ":" + direction, ConnectedRecords.Connect(items[other].Summary, null, direction, null));
        }
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in byKey)
        {
            string[] typeAndDirection = pair.Key.Split(':');
            string label = typeAndDirection[1] == "out" ? typeAndDirection[0] + " →" : "← " + typeAndDirection[0];
            groups.Add(ConnectedRecords.Group("relation:" + pair.Key, "relation", typeAndDirection[0], label, pair.Value));
        }
        return groups;
    }

    /// <summary>The entities that take part in the same events, by entity type, with how many events they share.</summary>
    private static List<ConnectionGroup> Together(string entityId, List<SampleItem> events, Dictionary<string, SampleItem> items)
    {
        // 1. Count the shared events of every other entity.
        Dictionary<string, int> sharedById = new Dictionary<string, int>();
        foreach (SampleItem happening in events)
        {
            foreach (EntityLabel participant in happening.Summary.Participants)
            {
                if (participant.Id != entityId && items.ContainsKey(participant.Id))
                {
                    sharedById[participant.Id] = sharedById.ContainsKey(participant.Id) ? sharedById[participant.Id] + 1 : 1;
                }
            }
        }

        // 2. Group them by their type.
        SortedDictionary<string, List<ConnectedRecord>> byType = new SortedDictionary<string, List<ConnectedRecord>>();
        foreach (KeyValuePair<string, int> pair in sharedById)
        {
            RecordSummary other = items[pair.Key].Summary;
            Add(byType, other.Type, ConnectedRecords.Connect(other, null, null, pair.Value));
        }
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in byType)
        {
            groups.Add(ConnectedRecords.Group("together:" + pair.Key, "together", pair.Key, "Shares events with · " + pair.Key, pair.Value));
        }
        return groups;
    }

    /// <summary>The events the entity takes part in, by event type, newest first, with its role.</summary>
    private static List<ConnectionGroup> Events(string entityId, List<SampleItem> events)
    {
        SortedDictionary<string, List<ConnectedRecord>> byType = new SortedDictionary<string, List<ConnectedRecord>>();
        foreach (SampleItem happening in events)
        {
            string role = ConnectedRecords.RolesOf((EventRecord)happening.Record, entityId);
            Add(byType, happening.Summary.Type, ConnectedRecords.Connect(happening.Summary, role, null, null));
        }
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in byType)
        {
            groups.Add(ConnectedRecords.Group("event:" + pair.Key, "event", pair.Key, "Takes part in · " + pair.Key, pair.Value));
        }
        return groups;
    }

    /// <summary>The entities taking part in the event (known at as_of), by role.</summary>
    private static List<ConnectionGroup> Participants(EventRecord happening, Dictionary<string, SampleItem> items)
    {
        SortedDictionary<string, List<ConnectedRecord>> byRole = new SortedDictionary<string, List<ConnectedRecord>>();
        foreach (Participant participant in happening.Participants)
        {
            if (participant.Entity != null && participant.Role != null && items.ContainsKey(participant.Entity))
            {
                Add(byRole, participant.Role, ConnectedRecords.Connect(items[participant.Entity].Summary, participant.Role, null, null));
            }
        }
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in byRole)
        {
            groups.Add(ConnectedRecords.Group("participant:" + pair.Key, "participant", null, "Taking part as " + pair.Key, pair.Value));
        }
        return groups;
    }

    /// <summary>The events that pass the filters and in which the entity takes part.</summary>
    private static List<SampleItem> EventsOf(string entityId, Dictionary<string, SampleItem> items, AnalyzeFilters filters)
    {
        List<SampleItem> events = new List<SampleItem>();
        foreach (SampleItem item in items.Values)
        {
            bool takesPart = item.Summary.Participants.Exists(participant => participant.Id == entityId);
            if (item.Summary.Kind == "event" && takesPart && SampleFilters.Matches(filters, item) && SampleFilters.InsidePlace(filters, item))
            {
                events.Add(item);
            }
        }
        return events;
    }

    /// <summary>Adds a connection to the list of its key.</summary>
    private static void Add(SortedDictionary<string, List<ConnectedRecord>> byKey, string key, ConnectedRecord connection)
    {
        if (!byKey.ContainsKey(key))
        {
            byKey.Add(key, new List<ConnectedRecord>());
        }
        byKey[key].Add(connection);
    }
}
