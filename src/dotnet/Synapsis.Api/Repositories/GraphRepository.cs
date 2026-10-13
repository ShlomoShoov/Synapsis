using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Results;
using Synapsis.Data.Dal;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Neo4j for the Analyze routes: the connections of a point (direct relations, entities that take part in the same
/// events). Every query builds its conditions with the existing GraphFilters predicates.
/// </summary>
public sealed class GraphRepository
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the repository over the Neo4j connection (each query opens a session with it).</summary>
    public GraphRepository(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }

    /// <summary>Reads direct relations, connected events, and entities sharing events with one entity.</summary>
    public async Task<List<GraphConnectionCandidate>> FindEntityConnectionsAsync(string entityId, AnalyzeFilters filters)
    {
        await using IAsyncSession session = _neo4jContext.OpenSession();
        List<GraphConnectionCandidate> candidates = new List<GraphConnectionCandidate>();

        // 1. Direct entity relations.
        Dictionary<string, object?> relationParameters = new Dictionary<string, object?> { ["id"] = entityId };
        string relationWhere = GraphFilters.ForLink(filters, "r", relationParameters);
        string relationQuery = "MATCH (center:Entity {id:$id})-[r:RELATED]-(other:Entity) " +
            "WHERE " + relationWhere + " " +
            "RETURN r.id AS relation_id, r.type AS relation_type, other.id AS neighbor_id, " +
            "CASE WHEN startNode(r).id = $id THEN 'out' ELSE 'in' END AS direction";
        candidates.AddRange(await ReadCandidatesAsync(session, relationQuery, relationParameters, "relation"));

        // 2. Events the center participates in.
        Dictionary<string, object?> eventParameters = new Dictionary<string, object?> { ["id"] = entityId };
        string eventWhere = GraphFilters.ForEvent(filters, "ev", eventParameters);
        string participationWhere = GraphFilters.ForLink(filters, "p", eventParameters);
        string eventQuery = "MATCH (center:Entity {id:$id})-[p:PARTICIPATES]->(ev:Event) " +
            "WHERE " + eventWhere + " AND " + participationWhere + " " +
            "RETURN ev.id AS event_id, collect(DISTINCT p.role) AS roles";
        candidates.AddRange(await ReadCandidatesAsync(session, eventQuery, eventParameters, "event"));

        // 3. Other entities sharing events with the center.
        Dictionary<string, object?> togetherParameters = new Dictionary<string, object?> { ["id"] = entityId };
        string togetherEventWhere = GraphFilters.ForEvent(filters, "ev", togetherParameters);
        string centerParticipantWhere = GraphFilters.ForLink(filters, "p", togetherParameters);
        string otherParticipantWhere = GraphFilters.ForLink(filters, "other_p", togetherParameters);
        string togetherQuery = "MATCH (center:Entity {id:$id})-[p:PARTICIPATES]->(ev:Event)<-[other_p:PARTICIPATES]-(other:Entity) " +
            "WHERE other.id <> $id AND " + togetherEventWhere + " AND " + centerParticipantWhere + " AND " + otherParticipantWhere + " " +
            "RETURN other.id AS neighbor_id, collect(DISTINCT ev.id) AS event_ids";
        candidates.AddRange(await ReadCandidatesAsync(session, togetherQuery, togetherParameters, "together"));
        return candidates;
    }

    /// <summary>Reads one fixed query shape and rejects missing required values instead of inventing defaults.</summary>
    private static async Task<List<GraphConnectionCandidate>> ReadCandidatesAsync(IAsyncSession session, string statement, Dictionary<string, object?> parameters, string kind)
    {
        IResultCursor cursor = await session.RunAsync(statement, parameters);
        List<IRecord> rows = await cursor.ToListAsync();
        List<GraphConnectionCandidate> candidates = new List<GraphConnectionCandidate>();
        foreach (IRecord row in rows)
        {
            GraphConnectionCandidate candidate = Candidate(row, kind);
            candidates.Add(candidate);
        }
        return candidates;
    }

    /// <summary>Maps a row's fixed query columns to its connection kind.</summary>
    private static GraphConnectionCandidate Candidate(IRecord row, string kind)
    {
        if (kind == "relation")
        {
            return new GraphConnectionCandidate
            {
                Kind = kind,
                NeighborId = RequiredString(row, "neighbor_id"),
                RelationId = RequiredString(row, "relation_id"),
                RelationType = RequiredString(row, "relation_type"),
                Direction = RequiredString(row, "direction")
            };
        }
        if (kind == "event")
        {
            string eventId = RequiredString(row, "event_id");
            return new GraphConnectionCandidate
            {
                Kind = kind,
                NeighborId = eventId,
                EventId = eventId,
                Roles = RequiredStrings(row, "roles")
            };
        }
        if (kind == "together")
        {
            return new GraphConnectionCandidate
            {
                Kind = kind,
                NeighborId = RequiredString(row, "neighbor_id"),
                EventIds = RequiredStrings(row, "event_ids")
            };
        }
        throw new InvalidOperationException("Unsupported graph connection query kind: " + kind);
    }

    /// <summary>Returns a required string column or reports the persisted graph data error.</summary>
    private static string RequiredString(IRecord row, string column)
    {
        if (!HasColumn(row, column) || row[column] == null)
        {
            throw new InvalidOperationException("Neo4j graph query is missing required value '" + column + "'.");
        }
        string? value = row[column].As<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Neo4j graph query returned an empty required value '" + column + "'.");
        }
        return value;
    }

    /// <summary>Returns a required list of strings and rejects null or non-string entries.</summary>
    private static List<string> RequiredStrings(IRecord row, string column)
    {
        if (!HasColumn(row, column) || row[column] == null)
        {
            throw new InvalidOperationException("Neo4j graph query is missing required list '" + column + "'.");
        }
        List<string> values = row[column].As<List<string>>();
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Neo4j graph query returned an empty value in '" + column + "'.");
            }
        }
        return values;
    }

    /// <summary>Checks that a query row contains one of its fixed aliases.</summary>
    private static bool HasColumn(IRecord row, string column)
    {
        foreach (string key in row.Keys)
        {
            if (key == column) return true;
        }
        return false;
    }

    /// <summary>
    /// The one Cypher statement of GET /graph: the node with this id (its label from the id's prefix, so the id index
    /// is used), its true number of links (degree, read from the store, not counted one by one), and at most limit of
    /// its links, any type, any direction, each with the node at the other end.
    /// </summary>
    public const string NeighboursStatement =
        "MATCH (c:{label} {id: $id}) " +
        "WITH c, COUNT { (c)--() } AS total " +
        "OPTIONAL MATCH (c)-[r]-(n) " +
        "WITH c, total, r, n LIMIT $limit " +
        "RETURN c {.*, labels: labels(c)} AS center, total, " +
        "collect({type: type(r), from: startNode(r).id, to: endNode(r).id, properties: properties(r), node: n {.*, labels: labels(n)}}) AS links";

    /// <summary>
    /// GET /graph: {center, total, links} for the record id; null when no node has it (or the id is not an entity,
    /// event or conclusion id). Values are passed through as Neo4j gives them; times become ISO text.
    /// </summary>
    public async Task<JsonObject?> NeighboursAsync(string id, int limit)
    {
        string? label = id.StartsWith("ent:", StringComparison.Ordinal) ? "Entity"
            : id.StartsWith("evt:", StringComparison.Ordinal) ? "Event"
            : id.StartsWith("con:", StringComparison.Ordinal) ? "Conclusion" : null;
        if (label == null)
        {
            return null;
        }
        await using IAsyncSession session = _neo4jContext.OpenSession();
        IResultCursor cursor = await session.RunAsync(NeighboursStatement.Replace("{label}", label), new Dictionary<string, object> { ["id"] = id, ["limit"] = limit });
        List<IRecord> rows = await cursor.ToListAsync();
        if (rows.Count == 0)
        {
            return null;
        }
        IRecord row = rows[0];
        JsonArray links = new JsonArray();
        foreach (object link in row["links"].As<List<object>>())
        {
            IDictionary<string, object> fields = (IDictionary<string, object>)link;
            if (fields["type"] != null)
            {
                links.Add(ToJson(link));
            }
        }
        return new JsonObject { ["center"] = ToJson(row["center"]), ["total"] = row["total"].As<long>(), ["links"] = links };
    }

    /// <summary>A Neo4j value as JSON: maps and lists as they are, times as ISO text, numbers, text and booleans as they are.</summary>
    private static JsonNode? ToJson(object? value)
    {
        if (value == null)
        {
            return null;
        }
        if (value is IDictionary<string, object> map)
        {
            JsonObject json = new JsonObject();
            foreach (KeyValuePair<string, object> pair in map)
            {
                json[pair.Key] = ToJson(pair.Value);
            }
            return json;
        }
        if (value is IList<object> list)
        {
            JsonArray json = new JsonArray();
            foreach (object item in list)
            {
                json.Add(ToJson(item));
            }
            return json;
        }
        if (value is ZonedDateTime zoned)
        {
            return zoned.ToDateTimeOffset().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
        if (value is string text)
        {
            return JsonValue.Create(text);
        }
        if (value is long whole)
        {
            return JsonValue.Create(whole);
        }
        if (value is double number)
        {
            return JsonValue.Create(number);
        }
        if (value is bool flag)
        {
            return JsonValue.Create(flag);
        }
        return value.ToString();
    }
}
