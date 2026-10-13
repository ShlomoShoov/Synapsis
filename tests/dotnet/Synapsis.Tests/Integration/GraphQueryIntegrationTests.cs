using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Controllers;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Results;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Repositories;
using Synapsis.Api.Services;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Options;
using Synapsis.Data.Models.Records;
using Synapsis.Neo4j;
using Xunit;

namespace Synapsis.Tests.Integration;

/// <summary>Checks graph reads against isolated MongoDB and Neo4j integration stores.</summary>
public sealed class GraphQueryIntegrationTests
{
    private static readonly DateTime KnownAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Mongo selects the highest state known at the requested time for every source.</summary>
    [Fact]
    public async Task As_of_many_selects_latest_known_state_for_each_source()
    {
        if (!MongoConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        string recordId = NewId("ent");
        await mongo.Records.InsertManyAsync(new List<StoredRecord>
        {
            Stored(recordId, "src:first", 1, KnownAt, EntityJson(recordId, "First state")),
            Stored(recordId, "src:first", 2, KnownAt.AddMonths(2), EntityJson(recordId, "Future state")),
            Stored(recordId, "src:first", 3, KnownAt.AddMonths(1), EntityJson(recordId, "Known state")),
            Stored(recordId, "src:second", 1, KnownAt, EntityJson(recordId, "Other source"))
        });
        RecordRepository repository = new RecordRepository(mongo);

        Dictionary<string, List<StoredRecord>> records = await repository.AsOfManyAsync(
            new List<string> { recordId }, KnownAt.AddMonths(1).AddDays(1));

        Assert.Equal(2, records[recordId].Count);
        Assert.Equal(3, records[recordId][0].State);
        Assert.Equal("src:first", records[recordId][0].Source);
        Assert.Equal("src:second", records[recordId][1].Source);
    }

    /// <summary>Empty candidate input returns no Mongo query results.</summary>
    [Fact]
    public async Task As_of_many_returns_empty_for_empty_input()
    {
        if (!MongoConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        RecordRepository repository = new RecordRepository(mongo);

        Dictionary<string, List<StoredRecord>> records = await repository.AsOfManyAsync(
            new List<string>(), KnownAt);

        Assert.Empty(records);
    }

    /// <summary>A MongoDB query failure remains a store error instead of an empty result.</summary>
    [Fact]
    public async Task As_of_many_propagates_mongo_failures()
    {
        MongoOptions unavailable = new MongoOptions
        {
            Url = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=1000",
            Database = "graph_test"
        };
        RecordRepository repository = new RecordRepository(new MongoContext(unavailable));

        await Assert.ThrowsAnyAsync<Exception>(() => repository.AsOfManyAsync(new List<string> { NewId("ent") }, KnownAt));
    }

    /// <summary>A directed relation is returned with its direction when queried from either endpoint.</summary>
    [Fact]
    public async Task Entity_connections_return_relation_direction_from_center()
    {
        if (!Neo4jConfigured()) return;
        await using Neo4jContext context = new Neo4jContext(Neo4jOptions());
        Neo4jRepository writer = new Neo4jRepository(context);
        string from = NewId("ent");
        string to = NewId("ent");
        string relationId = NewId("rel");
        await writer.StoreAsync(new RelationRecord
        {
            Kind = "relation", Id = relationId, Type = "RELATED_TO", Source = "src:test",
            KnownAt = KnownAt, From = from, To = to
        }, CancellationToken.None);
        GraphRepository repository = new GraphRepository(context);

        List<GraphConnectionCandidate> outgoing = await repository.FindEntityConnectionsAsync(from, new AnalyzeFilters());
        List<GraphConnectionCandidate> incoming = await repository.FindEntityConnectionsAsync(to, new AnalyzeFilters());

        GraphConnectionCandidate outCandidate = Assert.Single(outgoing);
        GraphConnectionCandidate inCandidate = Assert.Single(incoming);
        Assert.Equal("relation", outCandidate.Kind);
        Assert.Equal(relationId, outCandidate.RelationId);
        Assert.Equal("out", outCandidate.Direction);
        Assert.Equal(from, inCandidate.NeighborId);
        Assert.Equal("in", inCandidate.Direction);
    }

    /// <summary>Multiple roles for one event stay one event candidate and shared events count distinct ids.</summary>
    [Fact]
    public async Task Entity_connections_deduplicate_roles_and_shared_event_ids()
    {
        if (!Neo4jConfigured()) return;
        await using Neo4jContext context = new Neo4jContext(Neo4jOptions());
        Neo4jRepository writer = new Neo4jRepository(context);
        string centerId = NewId("ent");
        string otherId = NewId("ent");
        string firstEventId = NewId("evt");
        string secondEventId = NewId("evt");
        await writer.StoreAsync(Event(firstEventId, centerId, otherId, true), CancellationToken.None);
        await writer.StoreAsync(Event(secondEventId, centerId, otherId, false), CancellationToken.None);
        GraphRepository repository = new GraphRepository(context);

        List<GraphConnectionCandidate> candidates = await repository.FindEntityConnectionsAsync(centerId, new AnalyzeFilters());

        List<GraphConnectionCandidate> events = CandidatesOfKind(candidates, "event");
        GraphConnectionCandidate shared = Assert.Single(CandidatesOfKind(candidates, "together"));
        Assert.Equal(2, events.Count);
        Assert.Contains(events, candidate => candidate.Roles.Count == 2);
        Assert.Equal(2, shared.EventIds.Count);
        Assert.Contains(firstEventId, shared.EventIds);
        Assert.Contains(secondEventId, shared.EventIds);
    }

    /// <summary>The current graph can retain a replaced participation role, so as_of is not an old topology snapshot.</summary>
    [Fact]
    public async Task Historical_graph_query_can_include_a_replaced_role()
    {
        if (!Neo4jConfigured()) return;
        await using Neo4jContext context = new Neo4jContext(Neo4jOptions());
        Neo4jRepository writer = new Neo4jRepository(context);
        string centerId = NewId("ent");
        string otherId = NewId("ent");
        string eventId = NewId("evt");
        EventRecord earlier = Event(eventId, centerId, otherId, false);
        earlier.Participants[0].Role = "actor";
        await writer.StoreAsync(earlier, CancellationToken.None);
        EventRecord later = Event(eventId, centerId, otherId, false);
        later.KnownAt = KnownAt.AddMonths(1);
        later.Participants[0].Role = "subject";
        await writer.StoreAsync(later, CancellationToken.None);
        GraphRepository repository = new GraphRepository(context);
        AnalyzeFilters filters = new AnalyzeFilters { AsOf = KnownAt.AddMonths(1).AddDays(1) };

        List<GraphConnectionCandidate> candidates = await repository.FindEntityConnectionsAsync(centerId, filters);

        GraphConnectionCandidate eventCandidate = Assert.Single(CandidatesOfKind(candidates, "event"));
        Assert.Contains("actor", eventCandidate.Roles);
        Assert.Contains("subject", eventCandidate.Roles);
    }

    /// <summary>A graph query against an unavailable Neo4j database propagates the store failure.</summary>
    [Fact]
    public async Task Entity_connections_propagate_neo4j_query_failures()
    {
        if (!Neo4jConfigured()) return;
        Neo4jOptions configured = Neo4jOptions();
        Neo4jOptions missingDatabase = new Neo4jOptions
        {
            Url = configured.Url,
            Username = configured.Username,
            Password = configured.Password,
            Database = "missing_" + Guid.NewGuid().ToString("N")
        };
        await using Neo4jContext context = new Neo4jContext(missingDatabase);
        GraphRepository repository = new GraphRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.FindEntityConnectionsAsync(NewId("ent"), new AnalyzeFilters()));
    }

    /// <summary>A missing Mongo center is reported even if Neo4j contains an entity placeholder for it.</summary>
    [Fact]
    public async Task Graph_center_missing_from_mongo_is_not_created_from_placeholder()
    {
        if (!MongoConfigured() || !Neo4jConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(Neo4jOptions());
        string centerId = NewId("ent");
        string neighborId = NewId("ent");
        Neo4jRepository writer = new Neo4jRepository(neo4j);
        await writer.StoreAsync(new RelationRecord
        {
            Kind = "relation", Id = NewId("rel"), Type = "RELATED_TO", Source = "src:test",
            KnownAt = KnownAt, From = centerId, To = neighborId
        }, CancellationToken.None);
        AnalyzeService service = Service(mongo, neo4j);

        AnalyzeController controller = new AnalyzeController(service);

        ActionResult<GraphAnswer> answer = await controller.Graph(new GraphRequest { Id = centerId });

        ObjectResult problem = Assert.IsType<ObjectResult>(answer.Result);
        Assert.Equal(404, problem.StatusCode);
    }

    /// <summary>A neighbor placeholder without a Mongo statement is omitted from the API answer.</summary>
    [Fact]
    public async Task Graph_drops_neighbor_placeholder_without_mongo_record()
    {
        if (!MongoConfigured() || !Neo4jConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(Neo4jOptions());
        string centerId = NewId("ent");
        string placeholderId = NewId("ent");
        await mongo.Records.InsertOneAsync(Stored(centerId, "src:test", 1, KnownAt, EntityJson(centerId, "Center")));
        Neo4jRepository writer = new Neo4jRepository(neo4j);
        await writer.StoreAsync(new RelationRecord
        {
            Kind = "relation", Id = NewId("rel"), Type = "RELATED_TO", Source = "src:test",
            KnownAt = KnownAt, From = centerId, To = placeholderId
        }, CancellationToken.None);
        AnalyzeService service = Service(mongo, neo4j);

        GraphAnswer answer = await service.GraphAsync(new GraphRequest { Id = centerId });

        Assert.Empty(answer.Connections);
        Assert.NotNull(answer.NoConnectionsReason);
    }

    /// <summary>Cursor paging returns twenty items, followed by five items for a group of twenty-five.</summary>
    [Fact]
    public async Task Graph_paginates_group_with_offset_cursor()
    {
        if (!MongoConfigured() || !Neo4jConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(Neo4jOptions());
        string centerId = NewId("ent");
        await mongo.Records.InsertOneAsync(Stored(centerId, "src:test", 1, KnownAt, EntityJson(centerId, "Center")));
        Neo4jRepository writer = new Neo4jRepository(neo4j);
        List<StoredRecord> neighbors = new List<StoredRecord>();
        for (int index = 0; index < 25; index++)
        {
            string neighborId = NewId("ent");
            string relationId = NewId("rel");
            neighbors.Add(Stored(neighborId, "src:test", 1, KnownAt, EntityJson(neighborId, "Neighbor " + index.ToString("D2"))));
            await writer.StoreAsync(new RelationRecord
            {
                Kind = "relation", Id = relationId, Type = "RELATED_TO", Source = "src:test",
                KnownAt = KnownAt, From = centerId, To = neighborId
            }, CancellationToken.None);
        }
        await mongo.Records.InsertManyAsync(neighbors);
        AnalyzeService service = Service(mongo, neo4j);

        GraphAnswer first = await service.GraphAsync(new GraphRequest { Id = centerId, Limit = 20 });
        string group = Assert.Single(first.Connections).Key;
        GraphAnswer second = await service.GraphAsync(new GraphRequest { Id = centerId, Group = group, Limit = 20, Cursor = "20" });

        Assert.Equal(25, first.Connections[0].Count);
        Assert.Equal(20, first.Connections[0].Items.Count);
        Assert.Equal("20", first.Connections[0].Next);
        Assert.Equal(5, second.Connections[0].Items.Count);
        Assert.Null(second.Connections[0].Next);
    }

    /// <summary>An unknown connection group becomes a validation problem naming group.</summary>
    [Fact]
    public async Task Graph_unknown_group_returns_validation_problem()
    {
        if (!MongoConfigured() || !Neo4jConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(Neo4jOptions());
        string centerId = NewId("ent");
        await mongo.Records.InsertOneAsync(Stored(centerId, "src:test", 1, KnownAt, EntityJson(centerId, "Center")));
        AnalyzeService service = Service(mongo, neo4j);
        AnalyzeController controller = new AnalyzeController(service);

        ActionResult<GraphAnswer> answer = await controller.Graph(new GraphRequest { Id = centerId, Group = "unknown" });

        ObjectResult problem = Assert.IsType<ObjectResult>(answer.Result);
        Assert.Equal(400, problem.StatusCode);
        Assert.Contains("group", controller.ModelState.Keys);
    }

    /// <summary>An event's Mongo participant appears even when no Neo4j event node was stored.</summary>
    [Fact]
    public async Task Graph_event_center_reads_participants_from_mongo()
    {
        if (!MongoConfigured() || !Neo4jConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(Neo4jOptions());
        string eventId = NewId("evt");
        string participantId = NewId("ent");
        await mongo.Records.InsertManyAsync(new List<StoredRecord>
        {
            Stored(eventId, "src:test", 1, KnownAt, EventJson(eventId, participantId, "subject")),
            Stored(participantId, "src:test", 1, KnownAt, EntityJson(participantId, "Participant"))
        });
        AnalyzeService service = Service(mongo, neo4j);

        GraphAnswer answer = await service.GraphAsync(new GraphRequest { Id = eventId });

        ConnectionGroup group = Assert.Single(answer.Connections);
        ConnectedRecord item = Assert.Single(group.Items);
        Assert.Equal("participant", group.Kind);
        Assert.Equal(participantId, item.Id);
        Assert.Equal("subject", item.Role);
    }

    /// <summary>A conclusion loads known about entities for its statement label at the requested time.</summary>
    [Fact]
    public async Task Graph_conclusion_label_uses_known_about_entity_name()
    {
        if (!MongoConfigured()) return;
        MongoContext mongo = new MongoContext(MongoOptions());
        await using Neo4jContext neo4j = new Neo4jContext(new Neo4jOptions
        {
            Url = "bolt://127.0.0.1:1", Database = "neo4j"
        });
        string conclusionId = NewId("con");
        string entityId = NewId("ent");
        string missingId = NewId("ent");
        BsonDocument conclusion = new BsonDocument
        {
            ["kind"] = "conclusion", ["id"] = conclusionId, ["type"] = "TEST.CONCLUSION",
            ["source"] = "src:test", ["reliability"] = 80, ["known_at"] = new BsonDateTime(KnownAt),
            ["about"] = new BsonArray { entityId, missingId },
            ["statement"] = new BsonDocument { ["predicate"] = "in_logical_space", ["object"] = "LIBRARY" }
        };
        await mongo.Records.InsertManyAsync(new List<StoredRecord>
        {
            Stored(conclusionId, "src:test", 1, KnownAt, conclusion),
            Stored(entityId, "src:test", 1, KnownAt, EntityJson(entityId, "Apache"))
        });

        GraphAnswer answer = await Service(mongo, neo4j).GraphAsync(new GraphRequest { Id = conclusionId });

        Assert.Equal("Apache, " + missingId + ": in_logical_space LIBRARY", answer.Center.Label);
        Assert.Equal("statement", answer.Center.LabelFrom);
        Assert.Empty(answer.Connections);
    }

    /// <summary>Returns true when isolated MongoDB integration settings were supplied.</summary>
    private static bool MongoConfigured()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNAPSIS_MONGO_TEST_URL"));
    }

    /// <summary>Returns true when isolated Neo4j integration settings were supplied.</summary>
    private static bool Neo4jConfigured()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_URL"));
    }

    /// <summary>Creates MongoDB test connection settings.</summary>
    private static MongoOptions MongoOptions()
    {
        return new MongoOptions
        {
            Url = Environment.GetEnvironmentVariable("SYNAPSIS_MONGO_TEST_URL") ?? string.Empty,
            Database = Environment.GetEnvironmentVariable("SYNAPSIS_MONGO_TEST_DATABASE") ?? string.Empty
        };
    }

    /// <summary>Creates Neo4j test connection settings.</summary>
    private static Neo4jOptions Neo4jOptions()
    {
        return new Neo4jOptions
        {
            Url = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_URL"),
            Username = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_USERNAME"),
            Password = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_PASSWORD"),
            Database = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_DATABASE")
        };
    }

    /// <summary>Creates the Analyze service with its real MongoDB and Neo4j repositories.</summary>
    private static AnalyzeService Service(MongoContext mongo, Neo4jContext neo4j)
    {
        ElasticOptions elasticOptions = new ElasticOptions();
        SearchRepository searchRepository = new SearchRepository(new ElasticContext(elasticOptions), elasticOptions);
        return new AnalyzeService(searchRepository, new GraphRepository(neo4j), new RecordRepository(mongo));
    }

    /// <summary>Returns candidate rows of one graph kind without adding a LINQ dependency.</summary>
    private static List<GraphConnectionCandidate> CandidatesOfKind(List<GraphConnectionCandidate> candidates, string kind)
    {
        List<GraphConnectionCandidate> selected = new List<GraphConnectionCandidate>();
        foreach (GraphConnectionCandidate candidate in candidates)
        {
            if (candidate.Kind == kind) selected.Add(candidate);
        }
        return selected;
    }

    /// <summary>Stores an entity summary document without requiring Elasticsearch.</summary>
    private static BsonDocument EntityJson(string id, string name)
    {
        return new BsonDocument
        {
            ["kind"] = "entity", ["id"] = id, ["type"] = "Test.Entity", ["source"] = "src:test",
            ["reliability"] = 80, ["known_at"] = new BsonDateTime(KnownAt), ["name"] = name
        };
    }

    /// <summary>Stores an event record with one Mongo participant and role.</summary>
    private static BsonDocument EventJson(string id, string participantId, string role)
    {
        return new BsonDocument
        {
            ["kind"] = "event", ["id"] = id, ["type"] = "TEST.EVENT", ["source"] = "src:test",
            ["reliability"] = 80, ["known_at"] = new BsonDateTime(KnownAt),
            ["participants"] = new BsonArray { new BsonDocument { ["entity"] = participantId, ["role"] = role } }
        };
    }

    /// <summary>Creates the two-participant event used by connection query fixtures.</summary>
    private static EventRecord Event(string id, string centerId, string otherId, bool repeatedCenterRole)
    {
        EventRecord record = new EventRecord
        {
            Kind = "event", Id = id, Type = "TEST.EVENT", Source = "src:test", Reliability = 80,
            KnownAt = KnownAt, Time = new ValidTime { Start = KnownAt, Precision = "day" }
        };
        record.Participants.Add(new Participant { Entity = centerId, Role = "subject" });
        if (repeatedCenterRole)
        {
            record.Participants.Add(new Participant { Entity = centerId, Role = "observer" });
        }
        record.Participants.Add(new Participant { Entity = otherId, Role = "object" });
        return record;
    }

    /// <summary>Creates one Mongo state for the given identity and source.</summary>
    private static StoredRecord Stored(string id, string source, int state, DateTime knownAt, BsonDocument record)
    {
        return new StoredRecord
        {
            Key = StoredRecord.KeyOf(id, source) + "|" + state.ToString(),
            RecordId = id,
            Source = source,
            Kind = record["kind"].AsString,
            Type = record["type"].AsString,
            State = state,
            KnownAt = knownAt,
            ContentHash = Guid.NewGuid().ToString("N"),
            BatchId = Guid.NewGuid().ToString("N"),
            Producer = "test",
            ReceivedAt = knownAt,
            Record = record
        };
    }

    /// <summary>Creates a unique id so graph fixtures do not overlap other test runs.</summary>
    private static string NewId(string kind)
    {
        return kind + ":graph-test:" + Guid.NewGuid().ToString("N");
    }
}
