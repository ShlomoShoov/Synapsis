using System;
using System.Collections.Generic;
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
}
