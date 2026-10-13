using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Synapsis.Api.Models.Results;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Repositories;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;

namespace Synapsis.Api.Services;

/// <summary>
/// The logic of the Analyze routes: which stores to ask, in what order, and how to shape the answer. Elasticsearch
/// (SearchRepository) answers the map and the counts, Neo4j (GraphRepository) the connections, MongoDB
/// (RecordRepository) the center, candidate summaries, and event participants.
/// </summary>
public sealed class AnalyzeService
{
    private readonly SearchRepository _searchRepository;
    private readonly GraphRepository _graphRepository;
    private readonly RecordRepository _recordRepository;

    /// <summary>Creates the service over the three stores: Elasticsearch (map, search, counts), Neo4j (connections), MongoDB (an event's participants and roles).</summary>
    public AnalyzeService(SearchRepository searchRepository, GraphRepository graphRepository, RecordRepository recordRepository)
    {
        _searchRepository = searchRepository;
        _graphRepository = graphRepository;
        _recordRepository = recordRepository;
    }

    /// <summary>
    /// GET /analyze/map. The planned flow: 1. choose the layer (cells when zoomed out, below zoom 6, or when more
    /// than 2,000 located events match the filters; otherwise points); 2. ask Elasticsearch for the page of points or cell
    /// counts; 3. count what cannot be drawn (no point, too imprecise for the cell); 4. answer with the next cursor.
    /// </summary>
    public Task<MapAnswer> MapAsync(MapRequest request)
    {
        throw new NotImplementedException("GET /analyze/map is not built yet.");
    }

    /// <summary>GET /analyze/graph: select the center and connections at one moment, then group and page summaries.</summary>
    public async Task<GraphAnswer> GraphAsync(GraphRequest request)
    {
        // 1. Capture one UTC moment and find the center's selected Mongo states.
        DateTime asOf = request.AsOf == null ? DateTime.UtcNow : FilterParser.Utc(request.AsOf.Value);
        request.AsOf = asOf;
        List<StoredRecord> centerStatements = await _recordRepository.AsOfAsync(request.Id!, asOf, null);
        if (centerStatements.Count == 0)
        {
            string moment = asOf.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            throw new KeyNotFoundException("No record " + request.Id + " is known at " + moment + ".");
        }

        // 2. Find candidates from Neo4j or the selected event statement in MongoDB.
        List<GraphConnectionCandidate> candidates = await FindCandidatesAsync(request.Id!, centerStatements, request);

        // 3. Verify candidates in one batched Mongo read and build flat summaries.
        List<string> candidateIds = CandidateIds(candidates);
        Dictionary<string, List<StoredRecord>> statementsById = await _recordRepository.AsOfManyAsync(candidateIds, asOf);
        Dictionary<string, string> entityNames = EntityNames(centerStatements, statementsById, request.Id!);
        Dictionary<string, RecordSummary> summaries = Summaries(statementsById, entityNames);
        RecordSummary center = GraphSummaryBuilder.Build(centerStatements, entityNames);
        summaries[center.Id] = center;

        // 4. Group, order, and page candidates; an unknown group is a request error.
        List<ConnectionGroup> allGroups = BuildGroups(candidates, summaries, request);
        if (request.Group != null && !HasGroup(allGroups, request.Group))
        {
            throw new ArgumentException("group '" + request.Group + "' is not one of this record's connection groups.", "group");
        }
        List<ConnectionGroup> pages = Pages(allGroups, request);

        // 5. Explain an empty answer using the existing mock wording.
        string? noConnectionsReason = pages.Count == 0 ? WhyNoConnections(center, request) : null;
        return new GraphAnswer { AsOf = asOf, Center = center, Connections = pages, NoConnectionsReason = noConnectionsReason };
    }

    /// <summary>Finds entity links, Mongo event participants, or no links for a conclusion.</summary>
    private async Task<List<GraphConnectionCandidate>> FindCandidatesAsync(string centerId, List<StoredRecord> centerStatements, GraphRequest request)
    {
        List<GraphConnectionCandidate> candidates = new List<GraphConnectionCandidate>();
        SilRecord centerRecord = GraphSummaryBuilder.MostReliable(centerStatements).Record;
        if (centerRecord is EntityRecord)
        {
            return await _graphRepository.FindEntityConnectionsAsync(centerId, request);
        }
        if (centerRecord is EventRecord eventRecord)
        {
            foreach (Participant participant in eventRecord.Participants)
            {
                if (participant.Entity == null || participant.Role == null) continue;
                candidates.Add(new GraphConnectionCandidate { Kind = "participant", NeighborId = participant.Entity, Role = participant.Role });
            }
        }
        return candidates;
    }

    /// <summary>Collects unique neighbor and event ids for existence checks and shared-event counts.</summary>
    private static List<string> CandidateIds(List<GraphConnectionCandidate> candidates)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            ids.Add(candidate.NeighborId);
            if (candidate.EventId != null) ids.Add(candidate.EventId);
            foreach (string eventId in candidate.EventIds) ids.Add(eventId);
        }
        return new List<string>(ids);
    }

    /// <summary>Finds display names from entity statements, including the center when it is an entity.</summary>
    private static Dictionary<string, string> EntityNames(List<StoredRecord> centerStatements, Dictionary<string, List<StoredRecord>> statementsById, string centerId)
    {
        Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (GraphSummaryBuilder.MostReliable(centerStatements).Record is EntityRecord)
        {
            names[centerId] = GraphSummaryBuilder.Build(centerStatements, names).Label;
        }
        foreach (KeyValuePair<string, List<StoredRecord>> pair in statementsById)
        {
            if (GraphSummaryBuilder.MostReliable(pair.Value).Record is EntityRecord)
            {
                names[pair.Key] = GraphSummaryBuilder.Build(pair.Value, names).Label;
            }
        }
        return names;
    }

    /// <summary>Builds summaries only for records that MongoDB actually returned.</summary>
    private static Dictionary<string, RecordSummary> Summaries(Dictionary<string, List<StoredRecord>> statementsById, Dictionary<string, string> entityNames)
    {
        Dictionary<string, RecordSummary> summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<StoredRecord>> pair in statementsById)
        {
            summaries.Add(pair.Key, GraphSummaryBuilder.Build(pair.Value, entityNames));
        }
        return summaries;
    }

    /// <summary>Builds the relation, together, event, and participant groups in the agreed order.</summary>
    private static List<ConnectionGroup> BuildGroups(IReadOnlyList<GraphConnectionCandidate> candidates, IReadOnlyDictionary<string, RecordSummary> summaries, GraphRequest request)
    {
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        groups.AddRange(RelationGroups(candidates, summaries));
        groups.AddRange(TogetherGroups(candidates, summaries));
        groups.AddRange(EventGroups(candidates, summaries));
        groups.AddRange(ParticipantGroups(candidates, summaries));
        groups.Sort(CompareGroups);
        return groups;
    }

    /// <summary>Groups direct relation candidates by type and direction, retaining each relation once.</summary>
    private static List<ConnectionGroup> RelationGroups(IReadOnlyList<GraphConnectionCandidate> candidates, IReadOnlyDictionary<string, RecordSummary> summaries)
    {
        SortedDictionary<string, List<ConnectedRecord>> itemsByGroup = new SortedDictionary<string, List<ConnectedRecord>>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> seenByGroup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            if (candidate.Kind != "relation" || candidate.RelationType == null || candidate.Direction == null || !summaries.TryGetValue(candidate.NeighborId, out RecordSummary? summary) || summary.Kind != "entity") continue;
            string groupKey = "relation:" + candidate.RelationType + ":" + candidate.Direction;
            string identity = candidate.RelationId + "\0" + candidate.Direction + "\0" + candidate.NeighborId;
            if (!seenByGroup.ContainsKey(groupKey)) seenByGroup.Add(groupKey, new HashSet<string>(StringComparer.Ordinal));
            if (!seenByGroup[groupKey].Add(identity)) continue;
            Add(itemsByGroup, groupKey, Connect(summary, null, candidate.Direction, null));
        }
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in itemsByGroup)
        {
            string value = pair.Key.Substring("relation:".Length);
            int directionStart = value.LastIndexOf(':');
            string relationType = value.Substring(0, directionStart);
            string direction = value.Substring(directionStart + 1);
            string label = direction == "out" ? relationType + " →" : "← " + relationType;
            groups.Add(Group(pair.Key, "relation", relationType, label, pair.Value));
        }
        return groups;
    }

    /// <summary>Groups entities by type and counts unique shared event records that MongoDB confirmed.</summary>
    private static List<ConnectionGroup> TogetherGroups(IReadOnlyList<GraphConnectionCandidate> candidates, IReadOnlyDictionary<string, RecordSummary> summaries)
    {
        Dictionary<string, HashSet<string>> eventsByNeighbor = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            if (candidate.Kind != "together" || !summaries.TryGetValue(candidate.NeighborId, out RecordSummary? neighbor) || neighbor.Kind != "entity") continue;
            if (!eventsByNeighbor.ContainsKey(candidate.NeighborId)) eventsByNeighbor.Add(candidate.NeighborId, new HashSet<string>(StringComparer.Ordinal));
            foreach (string eventId in candidate.EventIds)
            {
                if (summaries.TryGetValue(eventId, out RecordSummary? eventSummary) && eventSummary.Kind == "event") eventsByNeighbor[candidate.NeighborId].Add(eventId);
            }
        }
        SortedDictionary<string, List<ConnectedRecord>> itemsByGroup = new SortedDictionary<string, List<ConnectedRecord>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, HashSet<string>> pair in eventsByNeighbor)
        {
            if (pair.Value.Count == 0) continue;
            RecordSummary summary = summaries[pair.Key];
            Add(itemsByGroup, "together:" + summary.Type, Connect(summary, null, null, pair.Value.Count));
        }
        return GroupsFrom(itemsByGroup, "together", "Shares events with · ");
    }

    /// <summary>Groups event candidates by type and joins their distinct roles in ordinal order.</summary>
    private static List<ConnectionGroup> EventGroups(IReadOnlyList<GraphConnectionCandidate> candidates, IReadOnlyDictionary<string, RecordSummary> summaries)
    {
        Dictionary<string, HashSet<string>> rolesByEvent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            if (candidate.Kind != "event" || !summaries.TryGetValue(candidate.NeighborId, out RecordSummary? summary) || summary.Kind != "event") continue;
            if (!rolesByEvent.ContainsKey(candidate.NeighborId)) rolesByEvent.Add(candidate.NeighborId, new HashSet<string>(StringComparer.Ordinal));
            foreach (string role in candidate.Roles) rolesByEvent[candidate.NeighborId].Add(role);
        }
        SortedDictionary<string, List<ConnectedRecord>> itemsByGroup = new SortedDictionary<string, List<ConnectedRecord>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, HashSet<string>> pair in rolesByEvent)
        {
            RecordSummary summary = summaries[pair.Key];
            List<string> roles = new List<string>(pair.Value);
            roles.Sort(string.CompareOrdinal);
            Add(itemsByGroup, "event:" + summary.Type, Connect(summary, string.Join(", ", roles), null, null));
        }
        return GroupsFrom(itemsByGroup, "event", "Takes part in · ");
    }

    /// <summary>Groups Mongo event participants by role and retains each entity-role pair once.</summary>
    private static List<ConnectionGroup> ParticipantGroups(IReadOnlyList<GraphConnectionCandidate> candidates, IReadOnlyDictionary<string, RecordSummary> summaries)
    {
        SortedDictionary<string, List<ConnectedRecord>> itemsByGroup = new SortedDictionary<string, List<ConnectedRecord>>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> seenByGroup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            if (candidate.Kind != "participant" || candidate.Role == null || !summaries.TryGetValue(candidate.NeighborId, out RecordSummary? summary) || summary.Kind != "entity") continue;
            string groupKey = "participant:" + candidate.Role;
            if (!seenByGroup.ContainsKey(groupKey)) seenByGroup.Add(groupKey, new HashSet<string>(StringComparer.Ordinal));
            if (!seenByGroup[groupKey].Add(candidate.NeighborId)) continue;
            Add(itemsByGroup, groupKey, Connect(summary, candidate.Role, null, null));
        }
        return GroupsFrom(itemsByGroup, "participant", "Taking part as ");
    }

    /// <summary>Creates groups from sorted keys with the label for that group kind.</summary>
    private static List<ConnectionGroup> GroupsFrom(SortedDictionary<string, List<ConnectedRecord>> itemsByGroup, string kind, string labelPrefix)
    {
        List<ConnectionGroup> groups = new List<ConnectionGroup>();
        foreach (KeyValuePair<string, List<ConnectedRecord>> pair in itemsByGroup)
        {
            string groupValue = pair.Key.Substring(kind.Length + 1);
            string? type = kind == "participant" ? null : groupValue;
            groups.Add(Group(pair.Key, kind, type, labelPrefix + groupValue, pair.Value));
        }
        return groups;
    }

    /// <summary>Orders items for one group and records its unpaged item count.</summary>
    private static ConnectionGroup Group(string key, string kind, string? type, string label, List<ConnectedRecord> items)
    {
        if (kind == "event") items.Sort(CompareNewestFirst);
        else if (kind == "together") items.Sort(CompareMostShared);
        else items.Sort(CompareByLabel);
        return new ConnectionGroup { Key = key, Kind = kind, Type = type, Label = label, Count = items.Count, Items = items };
    }

    /// <summary>Copies the flat record summary and sets the field for its connection kind.</summary>
    private static ConnectedRecord Connect(RecordSummary summary, string? role, string? direction, int? sharedEvents)
    {
        return new ConnectedRecord
        {
            Id = summary.Id, Kind = summary.Kind, Type = summary.Type, Sources = summary.Sources, KnownAt = summary.KnownAt,
            Label = summary.Label, LabelFrom = summary.LabelFrom, TimeStart = summary.TimeStart, TimeEnd = summary.TimeEnd,
            TimePrecision = summary.TimePrecision, Point = summary.Point, UncertaintyKm = summary.UncertaintyKm,
            Country = summary.Country, Subdivision = summary.Subdivision, AreaName = summary.AreaName, Logical = summary.Logical,
            Measures = summary.Measures, Participants = summary.Participants, Role = role, Direction = direction, SharedEvents = sharedEvents
        };
    }

    /// <summary>Adds an item to its group, creating the list on first use.</summary>
    private static void Add(SortedDictionary<string, List<ConnectedRecord>> groups, string key, ConnectedRecord item)
    {
        if (!groups.ContainsKey(key)) groups.Add(key, new List<ConnectedRecord>());
        groups[key].Add(item);
    }

    /// <summary>Pages every group or only the requested group; a cursor without a group is ignored.</summary>
    private static List<ConnectionGroup> Pages(List<ConnectionGroup> groups, GraphRequest request)
    {
        List<ConnectionGroup> pages = new List<ConnectionGroup>();
        foreach (ConnectionGroup group in groups)
        {
            if (request.Group != null && group.Key != request.Group) continue;
            string? cursor = request.Group == null ? null : request.Cursor;
            List<ConnectedRecord> items = Page(group.Items, cursor, request.Limit, out string? next);
            pages.Add(new ConnectionGroup { Key = group.Key, Kind = group.Kind, Type = group.Type, Label = group.Label, Count = group.Count, Items = items, Next = next });
        }
        return pages;
    }

    /// <summary>Returns one page with the mock's decimal offset cursor behavior.</summary>
    private static List<ConnectedRecord> Page(List<ConnectedRecord> ordered, string? cursor, int limit, out string? next)
    {
        int start = 0;
        if (cursor != null && int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out int position) && position < ordered.Count)
        {
            start = position;
        }
        int count = limit < ordered.Count - start ? limit : ordered.Count - start;
        int nextPosition = start + count;
        next = nextPosition < ordered.Count ? nextPosition.ToString(CultureInfo.InvariantCulture) : null;
        return ordered.GetRange(start, count);
    }

    /// <summary>Checks whether a requested group exists after filters and Mongo existence checks.</summary>
    private static bool HasGroup(List<ConnectionGroup> groups, string key)
    {
        foreach (ConnectionGroup group in groups)
        {
            if (group.Key == key) return true;
        }
        return false;
    }

    /// <summary>Sorts groups in the agreed kind order, then by ordinal key.</summary>
    private static int CompareGroups(ConnectionGroup first, ConnectionGroup second)
    {
        int firstOrder = GroupOrder(first.Kind);
        int secondOrder = GroupOrder(second.Kind);
        if (firstOrder != secondOrder) return firstOrder.CompareTo(secondOrder);
        return string.CompareOrdinal(first.Key, second.Key);
    }

    /// <summary>Returns the agreed group kind order.</summary>
    private static int GroupOrder(string kind)
    {
        if (kind == "relation") return 0;
        if (kind == "together") return 1;
        if (kind == "event") return 2;
        return 3;
    }

    /// <summary>Sorts event items newest first, then by record id.</summary>
    private static int CompareNewestFirst(ConnectedRecord first, ConnectedRecord second)
    {
        int byTime = Nullable.Compare(second.TimeStart, first.TimeStart);
        return byTime != 0 ? byTime : string.CompareOrdinal(first.Id, second.Id);
    }

    /// <summary>Sorts together items by shared event count, label, and id.</summary>
    private static int CompareMostShared(ConnectedRecord first, ConnectedRecord second)
    {
        int byCount = (second.SharedEvents ?? 0).CompareTo(first.SharedEvents ?? 0);
        return byCount != 0 ? byCount : CompareByLabel(first, second);
    }

    /// <summary>Sorts other items by label and then record id.</summary>
    private static int CompareByLabel(ConnectedRecord first, ConnectedRecord second)
    {
        int byLabel = string.CompareOrdinal(first.Label, second.Label);
        return byLabel != 0 ? byLabel : string.CompareOrdinal(first.Id, second.Id);
    }

    /// <summary>Uses the existing empty-answer sentence for the center kind and active filters.</summary>
    private static string WhyNoConnections(RecordSummary center, GraphRequest request)
    {
        if (center.Kind == "event") return "No entity takes part in this event, so it has no connections.";
        if (center.Kind == "conclusion") return "The links of a conclusion (what it is about, its premises) are not shown yet.";
        if (HasGraphFilters(request)) return "This entity has no relations, and none of its events pass the current filters.";
        return "No relation and no event of this entity is known at this as_of.";
    }

    /// <summary>Checks every shared filter except as_of when choosing the empty-answer sentence.</summary>
    private static bool HasGraphFilters(AnalyzeFilters filters)
    {
        bool anyList = filters.Kinds.Count + filters.Types.Count + filters.Sources.Count + filters.Cells.Count + filters.Countries.Count
            + filters.Subdivisions.Count + filters.LogicalNodes.Count + filters.Entities.Count + filters.Measures.Count > 0;
        return anyList || filters.Keywords != null || filters.From != null || filters.To != null || filters.BoundingBox != null;
    }
}
