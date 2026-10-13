using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;
using Synapsis.Data.Models.Records;
using Synapsis.Neo4j;
using Xunit;

namespace Synapsis.Tests.Integration;

/// <summary>Checks graph writes against a running, isolated Neo4j test database.</summary>
public sealed class Neo4jRepositoryTests
{
    private static readonly DateTimeOffset KnownAt = new DateTimeOffset(2023, 10, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Projects an entity and keeps one node after a repeated message.</summary>
    [Fact]
    public async Task Entity_writes_are_idempotent()
    {
        if (!Configured()) return;
        string id = NewId("ent");
        EntityRecord record = Entity(id, "First name");
        await StoreAsync(record);
        await StoreAsync(record);

        Assert.Equal(1, await CountAsync("MATCH (n:Entity {id: $id}) RETURN count(n) AS count", id));
        Assert.Equal("First name", await TextAsync("MATCH (n:Entity {id: $id}) RETURN n.name AS value", id));
    }

    /// <summary>Different sources share an entity node while retaining both sources and the earliest knowledge time.</summary>
    [Fact]
    public async Task Entity_sources_share_a_node_and_keep_the_earliest_time()
    {
        if (!Configured()) return;
        string id = NewId("ent");
        EntityRecord first = Entity(id, "First name");
        EntityRecord second = Entity(id, "New name");
        second.Source = "src:second";
        second.KnownAt = KnownAt.AddDays(1);
        await StoreAsync(first);
        await StoreAsync(second);

        Assert.Equal(2, await CountAsync("MATCH (n:Entity {id: $id}) RETURN size(n.sources) AS count", id));
        Assert.Equal("New name", await TextAsync("MATCH (n:Entity {id: $id}) RETURN n.name AS value", id));
        Assert.StartsWith("2023-10-18", await TextAsync("MATCH (n:Entity {id: $id}) RETURN toString(n.known_at) AS value", id));
    }

    /// <summary>Stores one event and one edge for each distinct entity and role.</summary>
    [Fact]
    public async Task Connecting_event_creates_event_and_participation_edges()
    {
        if (!Configured()) return;
        string id = NewId("evt");
        EventRecord record = Event(id, NewId("ent"), NewId("ent"));
        record.Participants.Add(new Participant { Entity = record.Participants[0].Entity, Role = "subject" });
        await StoreAsync(record);
        await StoreAsync(record);

        Assert.Equal(1, await CountAsync("MATCH (n:Event {id: $id}) RETURN count(n) AS count", id));
        Assert.Equal(2, await CountAsync("MATCH (:Entity)-[r:PARTICIPATES]->(n:Event {id: $id}) RETURN count(r) AS count", id));
        Assert.Equal("day", await TextAsync("MATCH (n:Event {id: $id}) RETURN n.precision AS value", id));
    }

    /// <summary>An event with one participant is not projected.</summary>
    [Fact]
    public async Task One_participant_event_stays_out_of_the_graph()
    {
        if (!Configured()) return;
        string id = NewId("evt");
        EventRecord record = Event(id, NewId("ent"), NewId("ent"));
        record.Participants.RemoveAt(1);
        await StoreAsync(record);

        Assert.Equal(0, await CountAsync("MATCH (n:Event {id: $id}) RETURN count(n) AS count", id));
    }

    /// <summary>A relation creates both missing endpoints and one directed edge.</summary>
    [Fact]
    public async Task Relation_creates_placeholders_and_one_directed_edge()
    {
        if (!Configured()) return;
        string id = NewId("rel");
        string from = NewId("ent");
        string to = NewId("ent");
        RelationRecord record = new RelationRecord { Id = id, Type = "ACQUIRED", Source = "src:test", KnownAt = KnownAt, From = from, To = to };
        await StoreAsync(record);
        await StoreAsync(record);

        Assert.Equal(1, await CountAsync("MATCH (:Entity {id: $from})-[r:RELATED {id: $id}]->(:Entity {id: $to}) RETURN count(r) AS count", id, from, to));
        Assert.Equal("ACQUIRED", await TextAsync("MATCH ()-[r:RELATED {id: $id}]->() RETURN r.type AS value", id));
        await StoreAsync(Entity(from, "Named later"));
        Assert.Equal("Named later", await TextAsync("MATCH (n:Entity {id: $id}) RETURN n.name AS value", from));
    }

    /// <summary>A conclusion retains missing targets and links them when they arrive.</summary>
    [Fact]
    public async Task Conclusion_links_targets_that_arrive_later()
    {
        if (!Configured()) return;
        string id = NewId("con");
        string entityId = NewId("ent");
        string eventId = NewId("evt");
        ConclusionRecord record = new ConclusionRecord
        {
            Id = id, Type = "spark", Source = "src:test", KnownAt = KnownAt,
            About = new List<string> { entityId, "subject:h3:test" },
            Premises = new List<string> { eventId },
            Statement = new Statement { Predicate = "anomaly" }, Confidence = 80
        };
        await StoreAsync(record);
        Assert.Equal(0, await CountAsync("MATCH (c:Conclusion {id: $id})-[r:ABOUT|PREMISE]->() RETURN count(r) AS count", id));

        await StoreAsync(Entity(entityId, "Target"));
        await StoreAsync(Event(eventId, NewId("ent"), NewId("ent")));
        Assert.Equal(1, await CountAsync("MATCH (c:Conclusion {id: $id})-[r:ABOUT]->(:Entity) RETURN count(r) AS count", id));
        Assert.Equal(1, await CountAsync("MATCH (c:Conclusion {id: $id})-[r:PREMISE]->(:Event) RETURN count(r) AS count", id));
    }

    /// <summary>A conclusion links existing targets and remains one node after a duplicate write.</summary>
    [Fact]
    public async Task Conclusion_links_existing_targets_without_duplicate_edges()
    {
        if (!Configured()) return;
        string id = NewId("con");
        string entityId = NewId("ent");
        await StoreAsync(Entity(entityId, "Already here"));
        ConclusionRecord record = new ConclusionRecord
        {
            Id = id, Type = "spark", Source = "src:test", KnownAt = KnownAt,
            About = new List<string> { entityId }, Statement = new Statement { Predicate = "anomaly" }
        };
        await StoreAsync(record);
        await StoreAsync(record);

        Assert.Equal(1, await CountAsync("MATCH (c:Conclusion {id: $id}) RETURN count(c) AS count", id));
        Assert.Equal(1, await CountAsync("MATCH (c:Conclusion {id: $id})-[r:ABOUT]->(:Entity) RETURN count(r) AS count", id));
        Assert.Equal("anomaly", await TextAsync("MATCH (c:Conclusion {id: $id}) RETURN c.predicate AS value", id));
    }

    /// <summary>An invalid participant prevents any part of its event transaction from being stored.</summary>
    [Fact]
    public async Task Invalid_participant_leaves_no_partial_event()
    {
        if (!Configured()) return;
        string id = NewId("evt");
        EventRecord record = Event(id, NewId("ent"), NewId("ent"));
        record.Participants[1].Role = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => StoreAsync(record));
        Assert.Equal(0, await CountAsync("MATCH (n:Event {id: $id}) RETURN count(n) AS count", id));
    }

    /// <summary>A failed transaction reports the error so Kafka cannot commit the message.</summary>
    [Fact]
    public async Task Invalid_database_reports_a_write_failure()
    {
        if (!Configured()) return;
        Neo4jOptions options = Options();
        Neo4jOptions missingDatabase = new Neo4jOptions
        {
            Url = options.Url, Username = options.Username, Password = options.Password,
            Database = "missing_" + Guid.NewGuid().ToString("N")
        };
        await using Neo4jContext context = new Neo4jContext(missingDatabase);
        Neo4jRepository repository = new Neo4jRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.StoreAsync(Entity(NewId("ent"), "Test"), CancellationToken.None));
    }

    /// <summary>Returns true only when integration-test connection settings were supplied.</summary>
    private static bool Configured() => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_URL"));

    /// <summary>Builds connection settings for the isolated Neo4j test instance.</summary>
    private static Neo4jOptions Options() => new Neo4jOptions
    {
        Url = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_URL"),
        Username = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_USERNAME"),
        Password = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_PASSWORD"),
        Database = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_DATABASE")
    };

    /// <summary>Stores one record using the real repository and driver.</summary>
    private static async Task StoreAsync(SilRecord record)
    {
        await using Neo4jContext context = new Neo4jContext(Options());
        Neo4jRepository repository = new Neo4jRepository(context);
        await repository.StoreAsync(record, CancellationToken.None);
    }

    /// <summary>Reads a count from the real graph.</summary>
    private static async Task<long> CountAsync(string query, string id, string? from = null, string? to = null)
    {
        IRecord result = await QueryAsync(query, id, from, to);
        return result["count"].As<long>();
    }

    /// <summary>Reads a text property from the real graph.</summary>
    private static async Task<string> TextAsync(string query, string id)
    {
        IRecord result = await QueryAsync(query, id);
        return result["value"].As<string>();
    }

    /// <summary>Runs one verification query against the test database.</summary>
    private static async Task<IRecord> QueryAsync(string query, string id, string? from = null, string? to = null)
    {
        Neo4jOptions options = Options();
        IDriver driver;
        if (string.IsNullOrWhiteSpace(options.Username) && string.IsNullOrWhiteSpace(options.Password))
        {
            driver = GraphDatabase.Driver(options.Url, AuthTokens.None);
        }
        else
        {
            driver = GraphDatabase.Driver(options.Url, AuthTokens.Basic(options.Username, options.Password));
        }
        await using (driver)
        {
            await using IAsyncSession session = driver.AsyncSession(config => config.WithDatabase(options.Database));
            IResultCursor cursor = await session.RunAsync(query, new { id, from, to });
            return await cursor.SingleAsync();
        }
    }

    /// <summary>Creates an entity record for a graph test.</summary>
    private static EntityRecord Entity(string id, string name) => new EntityRecord
    {
        Id = id, Type = "Vulnerability", Source = "src:test", KnownAt = KnownAt, Name = name
    };

    /// <summary>Creates an event with two distinct participants.</summary>
    private static EventRecord Event(string id, string first, string second) => new EventRecord
    {
        Id = id, Type = "TEST.EVENT", Source = "src:test", KnownAt = KnownAt,
        Time = new ValidTime { Start = KnownAt, Precision = "day" },
        Participants = new List<Participant>
        {
            new Participant { Entity = first, Role = "subject" },
            new Participant { Entity = second, Role = "object" }
        }
    };

    /// <summary>Creates an ID unique to one test run.</summary>
    private static string NewId(string kind) => kind + ":test:" + Guid.NewGuid().ToString("N");
}
