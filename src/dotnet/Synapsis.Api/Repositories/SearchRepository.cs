using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
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
