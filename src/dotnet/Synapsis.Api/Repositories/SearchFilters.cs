using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Services;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Turns the Analyze filters into the conditions of an Elasticsearch query on the records index (synapsis-records),
/// written once so every search and count uses them the same way. Each condition is the JSON Elasticsearch reads, as
/// in its documentation; a query puts the list in "bool": {"filter": [...]}. A filter that is not set adds nothing.
/// </summary>
public static class SearchFilters
{
    /// <summary>The fields keyword search reads (entity names, event and conclusion titles and bodies).</summary>
    private static readonly string[] TextFields = { "name", "title", "body" };

    /// <summary>Every condition the filters ask for, in a fixed order; empty when no filter is set.</summary>
    public static List<JsonObject> Build(AnalyzeFilters filters)
    {
        List<JsonObject> conditions = new List<JsonObject>();

        // 1. What was known by then.
        if (filters.AsOf != null)
        {
            conditions.Add(Range("known_at", "lte", Moment(filters.AsOf.Value)));
        }

        // 2. The words, and what the record is.
        if (!string.IsNullOrWhiteSpace(filters.Keywords))
        {
            conditions.Add(Keywords(filters.Keywords.Trim()));
        }
        AddTerms(conditions, "kind", filters.Kinds);
        AddTerms(conditions, "type", filters.Types);
        AddTerms(conditions, "source", filters.Sources);

        // 3. When it happened.
        if (filters.From != null || filters.To != null)
        {
            conditions.Add(TimeWindow(filters.From, filters.To));
        }

        // 4. Where it happened.
        if (filters.BoundingBox != null)
        {
            BoundingBox? box = FilterParser.ParseBoundingBox(filters.BoundingBox);
            if (box != null)
            {
                conditions.Add(GeoBox(box));
            }
        }
        if (filters.Cells.Count > 0)
        {
            conditions.Add(Cells(filters.Cells));
        }
        AddTerms(conditions, "country", filters.Countries);
        AddTerms(conditions, "subdivision", filters.Subdivisions);
        AddTerms(conditions, "logical_prefixes", filters.LogicalNodes);

        // 5. Who took part, and the numbers.
        AddTerms(conditions, "entities", filters.Entities);
        foreach (string text in filters.Measures)
        {
            MeasureCondition? measure = FilterParser.ParseMeasure(text);
            if (measure != null)
            {
                conditions.Add(Measure(measure));
            }
        }
        return conditions;
    }

    /// <summary>{"terms": {field: [values]}}: any of the values; nothing when the list is empty.</summary>
    private static void AddTerms(List<JsonObject> conditions, string field, List<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }
        JsonArray array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(value);
        }
        conditions.Add(new JsonObject { ["terms"] = new JsonObject { [field] = array } });
    }

    /// <summary>Every word in one of the text fields, or the whole text equal to the id (a CVE id typed in full).</summary>
    private static JsonObject Keywords(string text)
    {
        JsonArray fields = new JsonArray();
        foreach (string field in TextFields)
        {
            fields.Add(field);
        }
        JsonObject words = new JsonObject { ["multi_match"] = new JsonObject { ["query"] = text, ["fields"] = fields, ["operator"] = "and" } };
        JsonObject wholeId = new JsonObject { ["term"] = new JsonObject { ["id"] = text } };
        return new JsonObject { ["bool"] = new JsonObject { ["should"] = new JsonArray(words, wholeId), ["minimum_should_match"] = 1 } };
    }

    /// <summary>Events that start in the window: from included, to not.</summary>
    private static JsonObject TimeWindow(DateTime? from, DateTime? to)
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
        return new JsonObject { ["range"] = new JsonObject { ["time_start"] = bounds } };
    }

    /// <summary>Points inside the box (Elasticsearch handles a box that crosses the 180th meridian).</summary>
    private static JsonObject GeoBox(BoundingBox box)
    {
        JsonObject topLeft = new JsonObject { ["lat"] = box.MaxLatitude, ["lon"] = box.MinLongitude };
        JsonObject bottomRight = new JsonObject { ["lat"] = box.MinLatitude, ["lon"] = box.MaxLongitude };
        JsonObject corners = new JsonObject { ["top_left"] = topLeft, ["bottom_right"] = bottomRight };
        return new JsonObject { ["geo_bounding_box"] = new JsonObject { ["location"] = corners } };
    }

    /// <summary>Points in any of the cells: each cell is looked up in the field of its resolution (h3_r2 ... h3_r5).</summary>
    private static JsonObject Cells(List<string> cells)
    {
        JsonArray anyCell = new JsonArray();
        foreach (string cell in cells)
        {
            int? resolution = FilterParser.CellResolution(cell);
            if (resolution != null)
            {
                anyCell.Add(new JsonObject { ["term"] = new JsonObject { ["h3_r" + resolution.Value] = cell } });
            }
        }
        return new JsonObject { ["bool"] = new JsonObject { ["should"] = anyCell, ["minimum_should_match"] = 1 } };
    }

    /// <summary>A measure compared with a number; eq is a range from the number to the same number.</summary>
    private static JsonObject Measure(MeasureCondition measure)
    {
        string field = "measures." + measure.Name;
        if (measure.Operator == "eq")
        {
            return new JsonObject { ["range"] = new JsonObject { [field] = new JsonObject { ["gte"] = measure.Value, ["lte"] = measure.Value } } };
        }
        return Range(field, measure.Operator, measure.Value);
    }

    /// <summary>{"range": {field: {comparison: value}}}.</summary>
    private static JsonObject Range(string field, string comparison, JsonNode value)
    {
        return new JsonObject { ["range"] = new JsonObject { [field] = new JsonObject { [comparison] = value } } };
    }

    /// <summary>A moment as Elasticsearch reads it: UTC, to the second, such as "2024-05-01T00:00:00Z".</summary>
    private static string Moment(DateTime moment)
    {
        return FilterParser.Utc(moment).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
