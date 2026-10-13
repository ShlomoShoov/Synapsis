using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Elasticsearch for the read routes, on the records index (one document per state of a record, so records are counted
/// by distinct id, never by document). One method per route: it lists the conditions, then writes the whole search
/// body as it is sent, the JSON of the Elasticsearch documentation, and passes Elasticsearch's answer through.
/// </summary>
public sealed class SearchRepository
{
    private readonly ElasticsearchClient? _client;
    private readonly string _recordsIndex;
    private readonly EmbedderClient _embedderClient;

    /// <summary>
    /// Creates the repository over the Elasticsearch client (null when no address is set: every query then fails),
    /// the name of the records index ("synapsis-records") and the embedder (for search by meaning).
    /// </summary>
    public SearchRepository(ElasticContext elasticContext, ElasticOptions elasticOptions, EmbedderClient embedderClient)
    {
        _client = elasticContext.Client;
        _recordsIndex = elasticOptions.RecordsIndex;
        _embedderClient = embedderClient;
    }

    /// <summary>GET /map/cells: per H3 cell at the resolution, its documents and distinct records; and the records in all.</summary>
    public async Task<JsonObject> MapCellsAsync(int resolution, double[]? box, string kind, DateTime? from, DateTime? to, string[]? sources, string[]? types)
    {
        // 1. The conditions: the shared filters, the kind, a cell at this resolution, the box (when given).
        List<string> filters = SharedFilters(from, to, sources, types);
        filters.Add($$"""{ "term": { "kind": {{Json(kind)}} } }""");
        filters.Add($$"""{ "exists": { "field": "h3_r{{resolution}}" } }""");
        if (box != null)
        {
            filters.Add(InsideBox(box));
        }

        // 2. No documents ("size": 0): the cells, each with its distinct records, the most records first; the records in all.
        string body = $$"""
            {
              "size": 0,
              "query": { "bool": { "filter": [ {{string.Join(", ", filters)}} ] } },
              "aggs": {
                "cells": {
                  "terms": { "field": "h3_r{{resolution}}", "size": 10000, "order": { "records": "desc" } },
                  "aggs": { "records": { "cardinality": { "field": "id" } } }
                },
                "records": { "cardinality": { "field": "id" } }
              }
            }
            """;
        JsonNode aggregations = (await SendAsync(body))["aggregations"]!;

        // 3. The answer: the records in all, and the cells as Elasticsearch gives them.
        return new JsonObject
        {
            ["records"] = aggregations["records"]!["value"]!.DeepClone(),
            ["sum_other_doc_count"] = aggregations["cells"]!["sum_other_doc_count"]!.DeepClone(),
            ["buckets"] = aggregations["cells"]!["buckets"]!.DeepClone(),
        };
    }

    /// <summary>GET /map/points: the documents with a point in the box, one per record (its latest state); total = distinct records.</summary>
    public async Task<JsonObject> MapPointsAsync(double[] box, string kind, int limit, DateTime? from, DateTime? to, string[]? sources, string[]? types)
    {
        // 1. The conditions: the shared filters, the kind, the box.
        List<string> filters = SharedFilters(from, to, sources, types);
        filters.Add($$"""{ "term": { "kind": {{Json(kind)}} } }""");
        filters.Add(InsideBox(box));

        // 2. One document per record ("collapse" on id keeps the first by the sort: the latest state); the records in all.
        string body = $$"""
            {
              "size": {{limit}},
              "query": { "bool": { "filter": [ {{string.Join(", ", filters)}} ] } },
              "sort": [ { "state": "desc" } ],
              "collapse": { "field": "id" },
              "aggs": { "records": { "cardinality": { "field": "id" } } }
            }
            """;
        return TotalAndItems(await SendAsync(body));
    }

    /// <summary>
    /// GET /logical: one level of the logical tree. With a node, its direct children (the keys "node/one-segment");
    /// without, the top level (keys with no "/"). Each key with its documents and distinct records, at most 1,000.
    /// </summary>
    public async Task<JsonObject> LogicalAsync(string? node, DateTime? from, DateTime? to, string[]? sources, string[]? types)
    {
        // 1. The conditions: the shared filters only. The level: a regular expression (Lucene) the key must match whole.
        List<string> filters = SharedFilters(from, to, sources, types);
        string level = string.IsNullOrEmpty(node) ? "[^/]+" : LuceneLiteral(node) + "/[^/]+";

        // 2. No documents: the keys of the level, the most documents first, each with its distinct records.
        string body = $$"""
            {
              "size": 0,
              "query": { "bool": { "filter": [ {{string.Join(", ", filters)}} ] } },
              "aggs": {
                "level": {
                  "terms": { "field": "logical_prefixes", "size": 1000, "include": {{Json(level)}} },
                  "aggs": { "records": { "cardinality": { "field": "id" } } }
                }
              }
            }
            """;
        JsonNode aggregation = (await SendAsync(body))["aggregations"]!["level"]!;

        // 3. The answer: the buckets as Elasticsearch gives them.
        return new JsonObject
        {
            ["sum_other_doc_count"] = aggregation["sum_other_doc_count"]!.DeepClone(),
            ["buckets"] = aggregation["buckets"]!.DeepClone(),
        };
    }

    /// <summary>
    /// GET /search: with q, every word as the start of a word in the name, title or body, of the id or of the id's key
    /// (best first); with semantic, the k nearest by meaning; without q, the filtered list (newest first). One document
    /// per record; total = distinct records that match. Null when the embedder refuses the question.
    /// </summary>
    public async Task<JsonObject?> SearchAsync(string? q, bool semantic, string? kind, string? logical, string? entity, int limit,
        DateTime? from, DateTime? to, string[]? sources, string[]? types)
    {
        // 1. The conditions: the shared filters, and the kind, logical node and entity when given.
        List<string> filters = SharedFilters(from, to, sources, types);
        if (!string.IsNullOrEmpty(kind))
        {
            filters.Add($$"""{ "term": { "kind": {{Json(kind)}} } }""");
        }
        if (!string.IsNullOrEmpty(logical))
        {
            filters.Add($$"""{ "term": { "logical_prefixes": {{Json(logical)}} } }""");
        }
        if (!string.IsNullOrEmpty(entity))
        {
            filters.Add($$"""{ "term": { "entities": {{Json(entity)}} } }""");
        }

        // 2a. No words: the filtered list, the newest time_start first (records without one last).
        if (string.IsNullOrWhiteSpace(q))
        {
            string listBody = $$"""
                {
                  "size": {{limit}},
                  "query": { "bool": { "filter": [ {{string.Join(", ", filters)}} ] } },
                  "sort": [ { "time_start": { "order": "desc", "missing": "_last" } }, { "state": "desc" } ],
                  "collapse": { "field": "id" },
                  "aggs": { "records": { "cardinality": { "field": "id" } } }
                }
                """;
            return TotalAndItems(await SendAsync(listBody));
        }

        // 2b. Words: each word must be the start of a word in name, title or body, or the start of the id, or the start
        //     of the id's key (the part after the namespace: "CVE-2021-44228" finds "evt:kev:CVE-2021-44228").
        if (!semantic)
        {
            List<string> everyWord = new List<string>();
            foreach (string word in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                everyWord.Add($$"""
                    { "bool": { "should": [
                      { "multi_match": { "query": {{Json(word)}}, "type": "phrase_prefix", "fields": ["name", "title", "body"] } },
                      { "prefix": { "id": { "value": {{Json(word)}}, "case_insensitive": true } } },
                      { "prefix": { "id_key": { "value": {{Json(word)}}, "case_insensitive": true } } }
                    ] } }
                    """);
            }
            string wordsBody = $$"""
                {
                  "size": {{limit}},
                  "query": { "bool": { "filter": [ {{string.Join(", ", filters)}} ], "must": [ {{string.Join(", ", everyWord)}} ] } },
                  "collapse": { "field": "id" },
                  "aggs": { "records": { "cardinality": { "field": "id" } } }
                }
                """;
            return TotalAndItems(await SendAsync(wordsBody));
        }

        // 2c. Meaning: the question's vector from the embedder, then the k nearest (knn), the filters inside the knn.
        (float[]? vector, string? refusal) = await _embedderClient.EmbedAsync(q, CancellationToken.None);
        if (refusal != null)
        {
            return null;
        }
        if (vector == null)
        {
            return new JsonObject { ["total"] = 0, ["items"] = new JsonArray() };
        }
        string meaningBody = $$"""
            {
              "size": {{limit}},
              "knn": {
                "field": "text_vector", "query_vector": {{JsonSerializer.Serialize(vector)}},
                "k": {{limit}}, "num_candidates": 500,
                "filter": [ {{string.Join(", ", filters)}} ]
              },
              "collapse": { "field": "id" },
              "aggs": { "records": { "cardinality": { "field": "id" } } }
            }
            """;
        return TotalAndItems(await SendAsync(meaningBody));
    }

    /// <summary>The shared filters, each only when given: time_start in [from, to), any of the sources, any of the types.</summary>
    private static List<string> SharedFilters(DateTime? from, DateTime? to, string[]? sources, string[]? types)
    {
        List<string> filters = new List<string>();
        if (from != null)
        {
            filters.Add($$"""{ "range": { "time_start": { "gte": {{Json(Moment(from.Value))}} } } }""");
        }
        if (to != null)
        {
            filters.Add($$"""{ "range": { "time_start": { "lt": {{Json(Moment(to.Value))}} } } }""");
        }
        if (sources != null && sources.Length > 0)
        {
            filters.Add($$"""{ "terms": { "source": {{JsonSerializer.Serialize(sources)}} } }""");
        }
        if (types != null && types.Length > 0)
        {
            filters.Add($$"""{ "terms": { "type": {{JsonSerializer.Serialize(types)}} } }""");
        }
        return filters;
    }

    /// <summary>Points inside the box [minLon, minLat, maxLon, maxLat]; Elasticsearch handles a box across the 180th meridian (minLon > maxLon) itself.</summary>
    private static string InsideBox(double[] box)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{ "geo_bounding_box": { "location": { "top_left": { "lat": {{box[3]}}, "lon": {{box[0]}} }, "bottom_right": { "lat": {{box[1]}}, "lon": {{box[2]}} } } } }""");
    }

    /// <summary>Sends a search body to the records index and returns Elasticsearch's whole answer; fails on an error (500).</summary>
    private async Task<JsonNode> SendAsync(string body)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Elasticsearch has no address (setting Elastic:Url).");
        }
        StringResponse response = await _client.Transport.RequestAsync<StringResponse>(HttpMethod.POST, "/" + _recordsIndex + "/_search", PostData.String(body));
        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException("Elasticsearch answered " + response.ApiCallDetails.HttpStatusCode + ": " + response.Body);
        }
        return JsonNode.Parse(response.Body)!;
    }

    /// <summary>{"total": the distinct records (the "records" aggregation), "items": each hit's _source}.</summary>
    private static JsonObject TotalAndItems(JsonNode answer)
    {
        JsonArray items = new JsonArray();
        foreach (JsonNode? hit in answer["hits"]!["hits"]!.AsArray())
        {
            items.Add(hit!["_source"]!.DeepClone());
        }
        return new JsonObject { ["total"] = answer["aggregations"]!["records"]!["value"]!.DeepClone(), ["items"] = items };
    }

    /// <summary>A text as a JSON string, quoted and escaped: what a user typed can never break the body.</summary>
    private static string Json(string text)
    {
        return JsonSerializer.Serialize(text);
    }

    /// <summary>The text as a Lucene regular expression that matches exactly it: a backslash before every character that is not a letter or digit.</summary>
    private static string LuceneLiteral(string text)
    {
        StringBuilder literal = new StringBuilder();
        foreach (char character in text)
        {
            if (!char.IsLetterOrDigit(character))
            {
                literal.Append('\\');
            }
            literal.Append(character);
        }
        return literal.ToString();
    }

    /// <summary>A moment as Elasticsearch reads it, in UTC: "1965-01-01T00:00:00Z" (a date alone is UTC midnight; ASP.NET turns "…Z" into local time, turned back here).</summary>
    private static string Moment(DateTime moment)
    {
        DateTime utc = moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : moment;
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
