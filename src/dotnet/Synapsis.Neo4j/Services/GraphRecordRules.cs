using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;

namespace Synapsis.Neo4j;

/// <summary>Selects the participants of events that connect at least two distinct entities.</summary>
public static class GraphRecordRules
{
    /// <summary>Returns unique entity and role pairs, or an empty list when the event does not connect entities.</summary>
    public static List<Participant> ParticipantsOfConnectingEvent(EventRecord record)
    {
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
        if (entities.Count < 2)
        {
            return new List<Participant>();
        }
        return participants;
    }
}
