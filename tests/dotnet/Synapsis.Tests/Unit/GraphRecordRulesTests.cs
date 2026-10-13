using System;
using System.Collections.Generic;
using Synapsis.Data.Models.Records;
using Synapsis.Neo4j;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks which event participants make a useful graph connection.</summary>
public sealed class GraphRecordRulesTests
{
    /// <summary>An event with one entity (two roles) becomes a node since 13 Oct: an FDIC failure and its bank.</summary>
    [Fact]
    public void One_distinct_entity_creates_an_event_node()
    {
        EventRecord record = new EventRecord
        {
            Participants = new List<Participant>
            {
                new Participant { Entity = "ent:test:one", Role = "subject" },
                new Participant { Entity = "ent:test:one", Role = "observer" }
            }
        };

        Assert.Equal(2, GraphRecordRules.ParticipantsOfConnectingEvent(record).Count);
    }

    /// <summary>An event with no participant (an earthquake) does not become a node by itself.</summary>
    [Fact]
    public void No_participant_does_not_create_an_event_node()
    {
        Assert.Empty(GraphRecordRules.ParticipantsOfConnectingEvent(new EventRecord { Type = "GEO.EARTHQUAKE" }));
    }

    /// <summary>A type kept out of the graph (EPSS scores, NVD) does not become a node, even with a participant.</summary>
    [Theory]
    [InlineData("CYB.VULN.EPSS_SCORE")]
    [InlineData("CYB.VULN.PUBLISHED")]
    [InlineData("CYB.VULN.NVD_ASSESSMENT")]
    public void Kept_out_type_does_not_create_an_event_node(string type)
    {
        EventRecord record = new EventRecord
        {
            Type = type,
            Participants = new List<Participant> { new Participant { Entity = "ent:cve:CVE-2021-44228", Role = "subject" } }
        };

        Assert.Empty(GraphRecordRules.ParticipantsOfConnectingEvent(record));
    }

    /// <summary>A conclusion's targets are grouped by the label of their prefix, each once; other ids are left out.</summary>
    [Fact]
    public void Conclusion_targets_are_grouped_by_label()
    {
        Dictionary<string, List<string>> targets = GraphRecordRules.TargetsByLabel(new List<string>
        {
            "evt:usgs:a", "ent:h3:83225bfffffffff", "evt:usgs:a", "con:engine:b", "subject:h3:83225bfffffffff", "rel:x:c"
        });

        Assert.Equal(new List<string> { "evt:usgs:a" }, targets["Event"]);
        Assert.Equal(new List<string> { "ent:h3:83225bfffffffff" }, targets["Entity"]);
        Assert.Equal(new List<string> { "con:engine:b" }, targets["Conclusion"]);
        Assert.Equal(3, targets.Count);
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
