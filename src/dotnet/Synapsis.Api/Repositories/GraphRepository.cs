using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Dal;

namespace Synapsis.Api.Repositories;

/// <summary>Neo4j for GET /graph: a node and its direct links, in one Cypher statement.</summary>
public sealed class GraphRepository
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the repository over the Neo4j connection (each query opens a session with it).</summary>
    public GraphRepository(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }

    /// <summary>
    /// GET /graph: {center, total, links} for the record id, in one Cypher statement; null when no node has it (or the
    /// id is not an entity, event or conclusion id). Values are passed through as Neo4j gives them; times become ISO text.
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
        // The node by its label and id (so the id index is used); its true number of links (the degree Neo4j keeps
        // with the node); at most $limit of its links, any type, any direction, each with the node at the other end.
        string statement = $$$"""
            MATCH (c:{{{label}}} {id: $id})
            WITH c, COUNT { (c)--() } AS total
            OPTIONAL MATCH (c)-[r]-(n)
            WITH c, total, r, n LIMIT $limit
            RETURN c {.*, labels: labels(c)} AS center, total,
                   collect({type: type(r), from: startNode(r).id, to: endNode(r).id, properties: properties(r),
                            node: n {.*, labels: labels(n)}}) AS links
            """;
        await using IAsyncSession session = _neo4jContext.OpenSession();
        IResultCursor cursor = await session.RunAsync(statement, new Dictionary<string, object> { ["id"] = id, ["limit"] = limit });
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
