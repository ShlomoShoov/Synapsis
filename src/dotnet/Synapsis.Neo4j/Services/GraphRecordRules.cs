using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;

namespace Synapsis.Neo4j;

/// <summary>
/// Selects the participants of events that take part in the graph (at least one entity, except the types kept out),
/// and the graph label of the records a conclusion names.
/// </summary>
public static class GraphRecordRules
{
    /// <summary>
    /// Event types kept out of the graph even with a participant: too many to be useful as nodes (EPSS: about 12 M
    /// monthly scores, one per vulnerability per month). They stay searchable in Elasticsearch.
    /// </summary>
    private static readonly HashSet<string> TypesKeptOut = new HashSet<string> { "CYB.VULN.EPSS_SCORE" };

    /// <summary>The graph label of a record id, by its prefix (relations are links, not nodes, so "rel:" has none).</summary>
    private static readonly Dictionary<string, string> LabelByPrefix = new Dictionary<string, string>
    {
        ["evt:"] = "Event", ["ent:"] = "Entity", ["con:"] = "Conclusion"
    };

    /// <summary>
    /// Returns unique entity and role pairs, or an empty list when the event has no participant entity or its type is
    /// kept out of the graph. One entity is enough (an FDIC failure and its bank).
    /// </summary>
    public static List<Participant> ParticipantsOfConnectingEvent(EventRecord record)
    {
        // A type kept out never becomes a node.
        if (record.Type != null && TypesKeptOut.Contains(record.Type))
        {
            return new List<Participant>();
        }
        List<Participant> participants = new List<Participant>();
        HashSet<string> entities = new HashSet<string>();
        HashSet<string> pairs = new HashSet<string>();
        foreach (Participant participant in record.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.Entity) || string.IsNullOrWhiteSpace(participant.Role))
            {
                throw new InvalidOperationException("An event participant needs an entity and a role.");
            }
            entities.Add(participant.Entity);
            if (pairs.Add(participant.Entity + "\u001f" + participant.Role))
            {
                participants.Add(participant);
            }
        }

        // At least one entity (until 13 Oct: two, which left single-entity events such as FDIC reports out).
        if (entities.Count < 1)
        {
            return new List<Participant>();
        }
        return participants;
    }

    /// <summary>
    /// The ids a conclusion names (about or premises), grouped by the graph label their prefix gives (evt: Event, ent:
    /// Entity, con: Conclusion), each once. Other ids (a relation, a subject key such as "subject:h3:...") are left
    /// out: they have no node.
    /// </summary>
    public static Dictionary<string, List<string>> TargetsByLabel(List<string> ids)
    {
        Dictionary<string, List<string>> targets = new Dictionary<string, List<string>>();
        foreach (string id in ids)
        {
            foreach (KeyValuePair<string, string> prefix in LabelByPrefix)
            {
                if (!id.StartsWith(prefix.Key, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!targets.ContainsKey(prefix.Value))
                {
                    targets[prefix.Value] = new List<string>();
                }
                if (!targets[prefix.Value].Contains(id))
                {
                    targets[prefix.Value].Add(id);
                }
            }
        }
        return targets;
    }
}
