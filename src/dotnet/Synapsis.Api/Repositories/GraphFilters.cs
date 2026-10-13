using System.Collections.Generic;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Services;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Turns the Analyze filters into the conditions of a Cypher query (Neo4j), written once so every graph query uses
/// them the same way. The answer is the text after WHERE; the values go into the query's parameters ($as_of, $types,
/// ...), never into the text. The graph keeps only some fields of an event (type, source, start, known_at, who takes
/// part), so the filters on place, logical nodes, measures and keywords are not applied here.
/// </summary>
public static class GraphFilters
{
    /// <summary>
    /// The condition on an event node (such as "ev" in "MATCH (ev:Event)"): known by then, of the asked types and
    /// sources, started in the window, with one of the asked entities taking part. "true" when no filter applies.
    /// </summary>
    public static string ForEvent(AnalyzeFilters filters, string eventName, Dictionary<string, object?> parameters)
    {
        List<string> conditions = new List<string>();

        // 1. What was known by then.
        AddKnownBy(filters, eventName, parameters, conditions);

        // 2. What the record is: only events connect, so a kind filter without "event" keeps nothing.
        if (filters.Kinds.Count > 0 && !filters.Kinds.Contains("event"))
        {
            conditions.Add("false");
        }
        if (filters.Types.Count > 0)
        {
            conditions.Add(eventName + ".type IN $types");
            parameters["types"] = filters.Types;
        }
        if (filters.Sources.Count > 0)
        {
            conditions.Add(eventName + ".source IN $sources");
            parameters["sources"] = filters.Sources;
        }

        // 3. When it happened.
        if (filters.From != null)
        {
            conditions.Add(eventName + ".time_start >= $from");
            parameters["from"] = FilterParser.Utc(filters.From.Value);
        }
        if (filters.To != null)
        {
            conditions.Add(eventName + ".time_start < $to");
            parameters["to"] = FilterParser.Utc(filters.To.Value);
        }

        // 4. Who took part.
        if (filters.Entities.Count > 0)
        {
            conditions.Add("EXISTS { MATCH (taking:Entity)-[:PARTICIPATES]->(" + eventName + ") WHERE taking.id IN $entities }");
            parameters["entities"] = filters.Entities;
        }
        return Join(conditions);
    }

    /// <summary>
    /// The condition on a relation or a participation (a relationship such as "r" in "-[r:RELATED]-"): known by then.
    /// "true" without as_of.
    /// </summary>
    public static string ForLink(AnalyzeFilters filters, string linkName, Dictionary<string, object?> parameters)
    {
        List<string> conditions = new List<string>();
        AddKnownBy(filters, linkName, parameters, conditions);
        return Join(conditions);
    }

    /// <summary>"name.known_at &lt;= $as_of" when as_of is given (every node and edge carries known_at).</summary>
    private static void AddKnownBy(AnalyzeFilters filters, string name, Dictionary<string, object?> parameters, List<string> conditions)
    {
        if (filters.AsOf == null)
        {
            return;
        }
        conditions.Add(name + ".known_at <= $as_of");
        parameters["as_of"] = FilterParser.Utc(filters.AsOf.Value);
    }

    /// <summary>The conditions joined by AND; "true" when there are none, so "WHERE " + the answer is always valid.</summary>
    private static string Join(List<string> conditions)
    {
        if (conditions.Count == 0)
        {
            return "true";
        }
        return string.Join(" AND ", conditions);
    }
}
