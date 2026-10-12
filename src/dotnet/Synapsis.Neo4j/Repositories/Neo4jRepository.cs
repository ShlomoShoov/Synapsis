using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Records;

namespace Synapsis.Neo4j;

/// <summary>Writes the current graph view of each SIL record to Neo4j.</summary>
public sealed class Neo4jRepository
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the repository over the shared Neo4j driver.</summary>
    public Neo4jRepository(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }

    /// <summary>Creates the graph constraints and index before messages are processed.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string[] statements =
        {
            "CREATE CONSTRAINT entity_id IF NOT EXISTS FOR (n:Entity) REQUIRE n.id IS UNIQUE",
            "CREATE CONSTRAINT event_id IF NOT EXISTS FOR (n:Event) REQUIRE n.id IS UNIQUE",
            "CREATE CONSTRAINT conclusion_id IF NOT EXISTS FOR (n:Conclusion) REQUIRE n.id IS UNIQUE",
            "CREATE INDEX event_known_at IF NOT EXISTS FOR (n:Event) ON (n.known_at)"
        };
        await using IAsyncSession session = _neo4jContext.OpenSession();
        foreach (string statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IResultCursor result = await session.RunAsync(statement);
            await result.ConsumeAsync();
        }
    }

    /// <summary>Stores one record and all of its graph connections in one transaction.</summary>
    public async Task StoreAsync(SilRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.KnownAt == null || string.IsNullOrWhiteSpace(record.Id))
        {
            throw new InvalidOperationException("A graph record needs an id and known_at.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        await using IAsyncSession session = _neo4jContext.OpenSession();
        await session.ExecuteWriteAsync(async transaction =>
        {
            switch (record)
            {
                case EntityRecord entity:
                    await StoreEntityAsync(transaction, entity);
                    break;
                case EventRecord eventRecord:
                    await StoreEventAsync(transaction, eventRecord);
                    break;
                case RelationRecord relation:
                    await StoreRelationAsync(transaction, relation);
                    break;
                case ConclusionRecord conclusion:
                    await StoreConclusionAsync(transaction, conclusion);
                    break;
                default:
                    throw new InvalidOperationException("The record kind is not supported by the graph.");
            }
            return 0;
        });
    }

    /// <summary>Writes an entity and links conclusions that named it before it existed.</summary>
    private static async Task StoreEntityAsync(IAsyncQueryRunner transaction, EntityRecord record)
    {
        string statement = "MERGE (e:Entity {id: $id}) " +
            "SET e.type = $type, e.name = $name, e.aliases = $aliases, " +
            "e.sources = CASE WHEN $source IN coalesce(e.sources, []) THEN e.sources ELSE coalesce(e.sources, []) + $source END, " +
            "e.known_at = CASE WHEN e.known_at IS NULL OR e.known_at > $known_at THEN $known_at ELSE e.known_at END";
        await RunAsync(transaction, statement, new
        {
            id = record.Id, type = record.Type, name = record.Name, aliases = record.Aliases,
            source = record.Source, known_at = record.KnownAt!.Value.UtcDateTime
        });
        await LinkLateAboutAsync(transaction, record.Id!, "Entity");
    }

    /// <summary>Writes a connecting event, its participants and late conclusion links.</summary>
    private static async Task StoreEventAsync(IAsyncQueryRunner transaction, EventRecord record)
    {
        List<Participant> participants = GraphRecordRules.ParticipantsOfConnectingEvent(record);
        if (participants.Count == 0)
        {
            return;
        }
        DateTime knownAt = record.KnownAt!.Value.UtcDateTime;
        string statement = "MERGE (ev:Event {id: $id}) " +
            "SET ev.type = $type, ev.source = $source, ev.time_start = $time_start, " +
            "ev.time_end = $time_end, ev.precision = $precision, ev.known_at = $known_at, ev.title = $title";
        await RunAsync(transaction, statement, new
        {
            id = record.Id, type = record.Type, source = record.Source,
            time_start = record.Time?.Start?.UtcDateTime, time_end = record.Time?.End?.UtcDateTime,
            precision = record.Time?.Precision, known_at = knownAt, title = record.Text?.Title
        });
        foreach (Participant participant in participants)
        {
            await StoreParticipantAsync(transaction, record.Id!, participant, knownAt);
        }
        await LinkLateAboutAsync(transaction, record.Id!, "Event");
        await LinkLatePremisesAsync(transaction, record.Id!);
    }

    /// <summary>Writes a participant edge and its possibly unnamed entity.</summary>
    private static async Task StoreParticipantAsync(IAsyncQueryRunner transaction, string eventId, Participant participant, DateTime knownAt)
    {
        string statement = "MATCH (ev:Event {id: $event_id}) MERGE (en:Entity {id: $entity_id}) " +
            "SET en.known_at = CASE WHEN en.known_at IS NULL OR en.known_at > $known_at THEN $known_at ELSE en.known_at END " +
            "MERGE (en)-[r:PARTICIPATES {role: $role}]->(ev) SET r.known_at = $known_at";
        await RunAsync(transaction, statement, new
        {
            event_id = eventId, entity_id = participant.Entity, role = participant.Role, known_at = knownAt
        });
        await LinkLateAboutAsync(transaction, participant.Entity!, "Entity");
    }

    /// <summary>Writes a directed relation and its possibly unnamed endpoints.</summary>
    private static async Task StoreRelationAsync(IAsyncQueryRunner transaction, RelationRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.From) || string.IsNullOrWhiteSpace(record.To))
        {
            throw new InvalidOperationException("A relation needs from and to entity ids.");
        }
        string statement = "MERGE (a:Entity {id: $from}) MERGE (b:Entity {id: $to}) " +
            "SET a.known_at = CASE WHEN a.known_at IS NULL OR a.known_at > $known_at THEN $known_at ELSE a.known_at END, " +
            "b.known_at = CASE WHEN b.known_at IS NULL OR b.known_at > $known_at THEN $known_at ELSE b.known_at END " +
            "MERGE (a)-[r:RELATED {id: $id}]->(b) " +
            "SET r.type = $type, r.source = $source, r.time_start = $time_start, r.time_end = $time_end, r.known_at = $known_at";
        await RunAsync(transaction, statement, new
        {
            id = record.Id, from = record.From, to = record.To, type = record.Type, source = record.Source,
            time_start = record.Time?.Start?.UtcDateTime, time_end = record.Time?.End?.UtcDateTime,
            known_at = record.KnownAt!.Value.UtcDateTime
        });
        await LinkLateAboutAsync(transaction, record.From, "Entity");
        await LinkLateAboutAsync(transaction, record.To, "Entity");
    }

    /// <summary>Writes a conclusion and links targets already present in the graph.</summary>
    private static async Task StoreConclusionAsync(IAsyncQueryRunner transaction, ConclusionRecord record)
    {
        DateTime knownAt = record.KnownAt!.Value.UtcDateTime;
        string statement = "MERGE (c:Conclusion {id: $id}) " +
            "SET c.type = $type, c.predicate = $predicate, c.confidence = $confidence, " +
            "c.known_at = $known_at, c.title = $title, c.about = $about, c.premises = $premises";
        await RunAsync(transaction, statement, new
        {
            id = record.Id, type = record.Type, predicate = record.Statement?.Predicate,
            confidence = record.Confidence, known_at = knownAt, title = record.Text?.Title,
            about = record.About, premises = record.Premises
        });
        await LinkAboutAsync(transaction, record, "Entity");
        await LinkAboutAsync(transaction, record, "Event");
        await LinkPremisesAsync(transaction, record);
    }

    /// <summary>Links a conclusion to existing about targets of the chosen label.</summary>
    private static async Task LinkAboutAsync(IAsyncQueryRunner transaction, ConclusionRecord record, string label)
    {
        string statement = "MATCH (c:Conclusion {id: $id}) MATCH (target:" + label + ") " +
            "WHERE target.id IN $targets MERGE (c)-[r:ABOUT]->(target) SET r.known_at = $known_at";
        await RunAsync(transaction, statement, new { id = record.Id, targets = record.About, known_at = record.KnownAt!.Value.UtcDateTime });
    }

    /// <summary>Links a conclusion to existing premise events.</summary>
    private static async Task LinkPremisesAsync(IAsyncQueryRunner transaction, ConclusionRecord record)
    {
        string statement = "MATCH (c:Conclusion {id: $id}) MATCH (target:Event) " +
            "WHERE target.id IN $targets MERGE (c)-[r:PREMISE]->(target) SET r.known_at = $known_at";
        await RunAsync(transaction, statement, new { id = record.Id, targets = record.Premises, known_at = record.KnownAt!.Value.UtcDateTime });
    }

    /// <summary>Connects earlier conclusions that list a node in about.</summary>
    private static async Task LinkLateAboutAsync(IAsyncQueryRunner transaction, string targetId, string label)
    {
        string statement = "MATCH (target:" + label + " {id: $id}) MATCH (c:Conclusion) " +
            "WHERE $id IN c.about MERGE (c)-[r:ABOUT]->(target) SET r.known_at = c.known_at";
        await RunAsync(transaction, statement, new { id = targetId });
    }

    /// <summary>Connects earlier conclusions that list an event in premises.</summary>
    private static async Task LinkLatePremisesAsync(IAsyncQueryRunner transaction, string eventId)
    {
        string statement = "MATCH (target:Event {id: $id}) MATCH (c:Conclusion) " +
            "WHERE $id IN c.premises MERGE (c)-[r:PREMISE]->(target) SET r.known_at = c.known_at";
        await RunAsync(transaction, statement, new { id = eventId });
    }

    /// <summary>Runs and completes one parameterized Cypher statement inside the write transaction.</summary>
    private static async Task RunAsync(IAsyncQueryRunner transaction, string statement, object parameters)
    {
        IResultCursor result = await transaction.RunAsync(statement, parameters);
        await result.ConsumeAsync();
    }
}
