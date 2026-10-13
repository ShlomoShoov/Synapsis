using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Results;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Elasticsearch for the Analyze routes: the map and anything that is search or counting, on the records index
/// (one document per state of an entity, event or conclusion, so everything is counted by record id, never by
/// document). Every query starts from the shared conditions of SearchFilters and adds only its own; all of them go in
/// "bool": {"filter": [...]}. The queries are the JSON the Elasticsearch documentation writes, sent as is.
/// </summary>
public sealed class SearchRepository
{
    private readonly ElasticsearchClient? _client;
    private readonly string _recordsIndex;

    /// <summary>
    /// Creates the repository over the Elasticsearch client (null when no address is set: every query then fails)
    /// and the name of the records index ("synapsis-records").
    /// </summary>
    public SearchRepository(ElasticContext elasticContext, ElasticOptions elasticOptions)
    {
        _client = elasticContext.Client;
        _recordsIndex = elasticOptions.RecordsIndex;
    }

    /// <summary>
    /// The map's cells layer: the matching events counted per H3 cell of the resolution (by record id), the largest
    /// first and at most the request's limit, with the events and the cells in all.
    /// </summary>
    public async Task<CellCounts> CellCountsAsync(MapRequest request, int resolution)
    {
        // 1. The shared filters, plus the layer's own: events that have a cell at this resolution.
        string cellField = CellField(resolution);
        List<JsonObject> conditions = SearchFilters.Build(request);
        conditions.Add(Term("kind", "event"));
        conditions.Add(Exists(cellField));

        // 2. Per cell, the distinct record ids (the largest count first, then by cell); in all, the ids and the cells.
        JsonArray order = new JsonArray(new JsonObject { ["records"] = "desc" }, new JsonObject { ["_key"] = "asc" });
        JsonObject perCell = new JsonObject
        {
            ["terms"] = new JsonObject { ["field"] = cellField, ["size"] = request.Limit, ["order"] = order },
            ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        JsonObject aggregations = new JsonObject { ["cells"] = perCell, ["events"] = Cardinality("id"), ["total_cells"] = Cardinality(cellField) };
        JsonNode answer = await AggregateAsync(conditions, aggregations);

        // 3. Read the counts.
        Dictionary<string, int> countByCell = new Dictionary<string, int>();
        foreach (JsonNode? bucket in answer["cells"]!["buckets"]!.AsArray())
        {
            countByCell.Add(bucket!["key"]!.GetValue<string>(), bucket["records"]!["value"]!.GetValue<int>());
        }
        return new CellCounts { Events = ValueOf(answer["events"]!), TotalCells = ValueOf(answer["total_cells"]!), CountByCell = countByCell };
    }

    /// <summary>
    /// Matching events with no point (placed by region or logically only, or unplaced), by record id. The bbox and
    /// cell filters need a point, so with either set this is 0; to be solved with the logical places.
    /// </summary>
    public async Task<int> CountWithoutPointAsync(MapRequest request)
    {
        List<JsonObject> conditions = SearchFilters.Build(request);
        conditions.Add(Term("kind", "event"));
        conditions.Add(Missing("location"));
        return await CountRecordsAsync(conditions);
    }

    /// <summary>Matching events with a point but no cell at this resolution (the point is less precise than the cell), by record id.</summary>
    public async Task<int> CountTooImpreciseAsync(MapRequest request, int resolution)
    {
        List<JsonObject> conditions = SearchFilters.Build(request);
        conditions.Add(Term("kind", "event"));
        conditions.Add(Exists("location"));
        conditions.Add(Missing(CellField(resolution)));
        return await CountRecordsAsync(conditions);
    }

    // ---- The read routes (emergency mode): each method sends one search and passes Elasticsearch's answer through. ----

    /// <summary>The shared filters, each only when given: time_start in [from, to), any of the sources, any of the types, the kind.</summary>
    public static List<JsonObject> Filters(DateTime? from, DateTime? to, string[]? sources, string[]? types, string? kind)
    {
        List<JsonObject> filters = new List<JsonObject>();
        if (from != null || to != null)
        {
            JsonObject bounds = new JsonObject();
            if (from != null)
            {
                bounds["gte"] = Moment(from.Value);
            }
            if (to != null)
            {
                bounds["lt"] = Moment(to.Value);
            }
            filters.Add(new JsonObject { ["range"] = new JsonObject { ["time_start"] = bounds } });
        }
        if (sources != null && sources.Length > 0)
        {
            filters.Add(Terms("source", sources));
        }
        if (types != null && types.Length > 0)
        {
            filters.Add(Terms("type", types));
        }
        if (!string.IsNullOrEmpty(kind))
        {
            filters.Add(Term("kind", kind));
        }
        return filters;
    }

    /// <summary>Points inside the box [minLon, minLat, maxLon, maxLat]; Elasticsearch handles a box across the 180th meridian (minLon > maxLon) itself.</summary>
    public static JsonObject InsideBox(double[] box)
    {
        JsonObject topLeft = new JsonObject { ["lat"] = box[3], ["lon"] = box[0] };
        JsonObject bottomRight = new JsonObject { ["lat"] = box[1], ["lon"] = box[2] };
        return new JsonObject { ["geo_bounding_box"] = new JsonObject { ["location"] = new JsonObject { ["top_left"] = topLeft, ["bottom_right"] = bottomRight } } };
    }

    /// <summary>
    /// GET /map/cells: the documents that have a cell at this resolution, grouped by cell (terms, at most 10,000 cells),
    /// each with its distinct records (cardinality of id), the most records first; and the distinct records in all.
    /// </summary>
    public async Task<JsonObject> MapCellsAsync(List<JsonObject> filters, int resolution)
    {
        string cellField = CellField(resolution);
        filters.Add(Exists(cellField));
        JsonObject cells = new JsonObject
        {
            ["terms"] = new JsonObject { ["field"] = cellField, ["size"] = 10000, ["order"] = new JsonObject { ["records"] = "desc" } },
            ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        JsonObject body = new JsonObject { ["size"] = 0, ["query"] = Filtered(filters), ["aggs"] = new JsonObject { ["cells"] = cells, ["records"] = Cardinality("id") } };
        JsonNode answer = await PostSearchAsync(body);
        JsonNode aggregations = answer["aggregations"]!;
        return new JsonObject
        {
            ["records"] = aggregations["records"]!["value"]!.DeepClone(),
            ["sum_other_doc_count"] = aggregations["cells"]!["sum_other_doc_count"]!.DeepClone(),
            ["buckets"] = aggregations["cells"]!["buckets"]!.DeepClone(),
        };
    }

    /// <summary>
    /// GET /map/points (and /search with no words): the matching documents, one per record (collapse on id, its
    /// latest state), as their _source; total = the distinct records that match (cardinality of id).
    /// </summary>
    public async Task<JsonObject> ListAsync(List<JsonObject> filters, JsonArray sort, int limit)
    {
        JsonObject body = new JsonObject
        {
            ["size"] = limit, ["query"] = Filtered(filters), ["sort"] = sort,
            ["collapse"] = new JsonObject { ["field"] = "id" }, ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        return HitsOf(await PostSearchAsync(body));
    }

    /// <summary>
    /// GET /search?q=: every word of q found as the start of a word in the name, title or body, or as the start of
    /// the id; the best matches first, one per record (collapse on id); total = the distinct records that match.
    /// </summary>
    public async Task<JsonObject> SearchWordsAsync(List<JsonObject> filters, string words, int limit)
    {
        JsonArray everyWord = new JsonArray();
        foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonObject inText = new JsonObject { ["multi_match"] = new JsonObject { ["query"] = word, ["type"] = "phrase_prefix", ["fields"] = new JsonArray("name", "title", "body") } };
            JsonObject inId = new JsonObject { ["prefix"] = new JsonObject { ["id"] = new JsonObject { ["value"] = word, ["case_insensitive"] = true } } };
            everyWord.Add(new JsonObject { ["bool"] = new JsonObject { ["should"] = new JsonArray(inText, inId) } });
        }
        JsonObject query = Filtered(filters);
        query["bool"]!["must"] = everyWord;
        JsonObject body = new JsonObject
        {
            ["size"] = limit, ["query"] = query,
            ["collapse"] = new JsonObject { ["field"] = "id" }, ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        return HitsOf(await PostSearchAsync(body));
    }

    /// <summary>
    /// GET /search?semantic=true: the k nearest documents to the question's vector (knn on text_vector, 500 candidates),
    /// with the filters inside the knn, one per record; total = the distinct records among those k (never more than k).
    /// </summary>
    public async Task<JsonObject> SearchMeaningAsync(List<JsonObject> filters, float[] vector, int limit)
    {
        JsonArray queryVector = new JsonArray();
        foreach (float value in vector)
        {
            queryVector.Add(value);
        }
        JsonArray filter = new JsonArray();
        foreach (JsonObject condition in filters)
        {
            filter.Add(condition);
        }
        JsonObject knn = new JsonObject { ["field"] = "text_vector", ["query_vector"] = queryVector, ["k"] = limit, ["num_candidates"] = 500, ["filter"] = filter };
        JsonObject body = new JsonObject
        {
            ["size"] = limit, ["knn"] = knn,
            ["collapse"] = new JsonObject { ["field"] = "id" }, ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        return HitsOf(await PostSearchAsync(body));
    }

    /// <summary>
    /// GET /logical: one level of the logical tree. With a node, its direct children (the keys "node/one-segment");
    /// without, the top level (keys with no "/"). Each key with its documents and distinct records, at most 1,000, the
    /// most documents first.
    /// </summary>
    public async Task<JsonObject> LogicalAsync(List<JsonObject> filters, string? node)
    {
        string children = string.IsNullOrEmpty(node) ? "[^/]+" : LuceneLiteral(node) + "/[^/]+";
        JsonObject level = new JsonObject
        {
            ["terms"] = new JsonObject { ["field"] = "logical_prefixes", ["size"] = 1000, ["include"] = children },
            ["aggs"] = new JsonObject { ["records"] = Cardinality("id") },
        };
        JsonObject body = new JsonObject { ["size"] = 0, ["query"] = Filtered(filters), ["aggs"] = new JsonObject { ["level"] = level } };
        JsonNode answer = await PostSearchAsync(body);
        JsonNode aggregation = answer["aggregations"]!["level"]!;
        return new JsonObject { ["sum_other_doc_count"] = aggregation["sum_other_doc_count"]!.DeepClone(), ["buckets"] = aggregation["buckets"]!.DeepClone() };
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

    /// <summary>{"total": distinct records, "items": [each hit's _source]}.</summary>
    private static JsonObject HitsOf(JsonNode answer)
    {
        JsonArray items = new JsonArray();
        foreach (JsonNode? hit in answer["hits"]!["hits"]!.AsArray())
        {
            items.Add(hit!["_source"]!.DeepClone());
        }
        return new JsonObject { ["total"] = answer["aggregations"]!["records"]!["value"]!.DeepClone(), ["items"] = items };
    }

    /// <summary>{"bool": {"filter": [the conditions]}}.</summary>
    private static JsonObject Filtered(List<JsonObject> filters)
    {
        JsonArray filter = new JsonArray();
        foreach (JsonObject condition in filters)
        {
            filter.Add(condition);
        }
        return new JsonObject { ["bool"] = new JsonObject { ["filter"] = filter } };
    }

    /// <summary>Sends a search body to the records index and returns Elasticsearch's whole answer; fails on an error (500).</summary>
    private async Task<JsonNode> PostSearchAsync(JsonObject body)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Elasticsearch has no address (setting Elastic:Url).");
        }
        StringResponse response = await _client.Transport.RequestAsync<StringResponse>(HttpMethod.POST, "/" + _recordsIndex + "/_search", PostData.String(body.ToJsonString()));
        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException("Elasticsearch answered " + response.ApiCallDetails.HttpStatusCode + ": " + response.Body);
        }
        return JsonNode.Parse(response.Body)!;
    }

    /// <summary>{"terms": {field: [values]}}: any of the values.</summary>
    private static JsonObject Terms(string field, string[] values)
    {
        JsonArray array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(value);
        }
        return new JsonObject { ["terms"] = new JsonObject { [field] = array } };
    }

    /// <summary>A moment as Elasticsearch reads it, in UTC: "1965-01-01T00:00:00Z" (a date alone is UTC midnight; ASP.NET turns "…Z" into local time, turned back here).</summary>
    private static string Moment(DateTime moment)
    {
        DateTime utc = moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : moment;
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>How many distinct record ids match the conditions.</summary>
    private async Task<int> CountRecordsAsync(List<JsonObject> conditions)
    {
        JsonNode answer = await AggregateAsync(conditions, new JsonObject { ["records"] = Cardinality("id") });
        return ValueOf(answer["records"]!);
    }

    /// <summary>
    /// Runs a search that returns no documents, only the aggregations ("size": 0), and returns its "aggregations".
    /// Fails when Elasticsearch has no address or answers with an error (the route then answers 500).
    /// </summary>
    private async Task<JsonNode> AggregateAsync(List<JsonObject> conditions, JsonObject aggregations)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Elasticsearch has no address (setting Elastic:Url).");
        }
        JsonArray filter = new JsonArray();
        foreach (JsonObject condition in conditions)
        {
            filter.Add(condition);
        }
        JsonObject query = new JsonObject { ["bool"] = new JsonObject { ["filter"] = filter } };
        JsonObject body = new JsonObject { ["size"] = 0, ["query"] = query, ["aggs"] = aggregations };
        StringResponse response = await _client.Transport.RequestAsync<StringResponse>(HttpMethod.POST, "/" + _recordsIndex + "/_search", PostData.String(body.ToJsonString()));
        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException("Elasticsearch answered " + response.ApiCallDetails.HttpStatusCode + ": " + response.Body);
        }
        return JsonNode.Parse(response.Body)!["aggregations"]!;
    }

    /// <summary>The field of the H3 cell at a resolution, such as "h3_r3".</summary>
    private static string CellField(int resolution)
    {
        return "h3_r" + resolution;
    }

    /// <summary>{"term": {field: value}}: the field equals the value.</summary>
    private static JsonObject Term(string field, string value)
    {
        return new JsonObject { ["term"] = new JsonObject { [field] = value } };
    }

    /// <summary>{"exists": {"field": field}}: the field has a value.</summary>
    private static JsonObject Exists(string field)
    {
        return new JsonObject { ["exists"] = new JsonObject { ["field"] = field } };
    }

    /// <summary>The field has no value.</summary>
    private static JsonObject Missing(string field)
    {
        return new JsonObject { ["bool"] = new JsonObject { ["must_not"] = new JsonArray(Exists(field)) } };
    }

    /// <summary>{"cardinality": {"field": field}}: how many distinct values (approximate on large counts).</summary>
    private static JsonObject Cardinality(string field)
    {
        return new JsonObject { ["cardinality"] = new JsonObject { ["field"] = field } };
    }

    /// <summary>The "value" of a one-number aggregation.</summary>
    private static int ValueOf(JsonNode aggregation)
    {
        return aggregation["value"]!.GetValue<int>();
    }
}
