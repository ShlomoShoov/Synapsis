using System;
using System.Text.Json.Nodes;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Repositories.Mappers;

/// <summary>
/// The statistics query of the ingestion log and the reading of its answer. Elasticsearch sums the counts per source
/// ("terms" + "sum" aggregations) and per error code ("nested" aggregation over rejection_counts).
/// </summary>
public static class IngestStatsMapper
{
    /// <summary>The counts summed per source.</summary>
    private static readonly string[] Counts = { "received", "new", "changed", "unchanged", "duplicates", "rejected" };

    /// <summary>The aggregation query for the entries between two moments; no documents are returned, only totals.</summary>
    public static JsonNode Query(DateTime from, DateTime to)
    {
        JsonObject sums = new JsonObject();
        foreach (string count in Counts)
        {
            sums[count] = new JsonObject { ["sum"] = new JsonObject { ["field"] = count } };
        }
        JsonObject codes = new JsonObject
        {
            ["nested"] = new JsonObject { ["path"] = "rejection_counts" },
            ["aggs"] = new JsonObject
            {
                ["by_code"] = new JsonObject
                {
                    ["terms"] = new JsonObject { ["field"] = "rejection_counts.code", ["size"] = 200 },
                    ["aggs"] = new JsonObject { ["count"] = new JsonObject { ["sum"] = new JsonObject { ["field"] = "rejection_counts.count" } } },
                },
            },
        };
        return new JsonObject
        {
            ["size"] = 0,
            ["query"] = new JsonObject { ["range"] = new JsonObject { ["timestamp"] = new JsonObject { ["gte"] = from.ToString("O"), ["lte"] = to.ToString("O") } } },
            ["aggs"] = new JsonObject
            {
                ["sources"] = new JsonObject { ["terms"] = new JsonObject { ["field"] = "source", ["size"] = 500 }, ["aggs"] = sums },
                ["codes"] = codes,
            },
        };
    }

    /// <summary>Reads the aggregation answer into totals; an answer without aggregations (no index yet) gives empty totals.</summary>
    public static IngestStats ToStats(DateTime from, DateTime to, JsonNode answer)
    {
        IngestStats stats = new IngestStats { From = from, To = to };
        JsonNode? aggregations = answer["aggregations"];
        if (aggregations == null)
        {
            return stats;
        }
        foreach (JsonNode? bucket in aggregations["sources"]!["buckets"]!.AsArray())
        {
            stats.Sources.Add(ToSourceTotals(bucket!));
        }
        foreach (JsonNode? bucket in aggregations["codes"]!["by_code"]!["buckets"]!.AsArray())
        {
            stats.Codes.Add(new RejectionCount { Code = bucket!["key"]!.GetValue<string>(), Count = Sum(bucket, "count") });
        }
        return stats;
    }

    /// <summary>One source's bucket: its number of batches (documents) and its summed counts.</summary>
    private static SourceTotals ToSourceTotals(JsonNode bucket)
    {
        return new SourceTotals
        {
            Source = bucket["key"]!.GetValue<string>(),
            Batches = bucket["doc_count"]!.GetValue<long>(),
            Received = Sum(bucket, "received"),
            New = Sum(bucket, "new"),
            Changed = Sum(bucket, "changed"),
            Unchanged = Sum(bucket, "unchanged"),
            Duplicates = Sum(bucket, "duplicates"),
            Rejected = Sum(bucket, "rejected"),
        };
    }

    /// <summary>The value of a "sum" aggregation; Elasticsearch gives sums as decimals, the counts are whole numbers.</summary>
    private static long Sum(JsonNode bucket, string name)
    {
        return (long)bucket[name]!["value"]!.GetValue<double>();
    }
}
