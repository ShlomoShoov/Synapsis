using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.Neo4j;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks which event participants make a useful graph connection.</summary>
public sealed class GraphRecordRulesTests
{
    /// <summary>An event with two roles for one entity does not connect entities.</summary>
    [Fact]
    public void One_distinct_entity_does_not_create_an_event_node()
    {
        EventRecord record = new EventRecord
        {
            Participants = new List<Participant>
            {
                new Participant { Entity = "ent:test:one", Role = "subject" },
                new Participant { Entity = "ent:test:one", Role = "observer" }
            }
        };

        Assert.Empty(GraphRecordRules.ParticipantsOfConnectingEvent(record));
    }

    /// <summary>Repeated identical participants become one graph edge.</summary>
    [Fact]
    public void Duplicate_entity_and_role_pairs_are_stored_once()
    {
        EventRecord record = new EventRecord
        {
            Participants = new List<Participant>
            {
                new Participant { Entity = "ent:test:one", Role = "subject" },
                new Participant { Entity = "ent:test:one", Role = "subject" },
                new Participant { Entity = "ent:test:two", Role = "object" }
            }
        };

        Assert.Equal(2, GraphRecordRules.ParticipantsOfConnectingEvent(record).Count);
    }

    /// <summary>A missing role is reported instead of becoming a graph value.</summary>
    [Fact]
    public void Missing_participant_role_is_reported()
    {
        EventRecord record = new EventRecord
        {
            Participants = new List<Participant>
            {
                new Participant { Entity = "ent:test:one" },
                new Participant { Entity = "ent:test:two", Role = "object" }
            }
        };

        Assert.Throws<InvalidOperationException>(() => GraphRecordRules.ParticipantsOfConnectingEvent(record));
    }
}
