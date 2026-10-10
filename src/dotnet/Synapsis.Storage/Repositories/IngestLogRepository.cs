using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories.Mappers;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// Keeps the ingestion log in the Elasticsearch index "synapsis-ingest-log". The index is created with an explicit
/// mapping the first time an entry is written. Queries are written as the JSON Elasticsearch documents show.
/// </summary>
public sealed class IngestLogRepository : IIngestLogRepository
{
    /// <summary>The index name.</summary>
    public const string Index = "synapsis-ingest-log";

    /// <summary>Field names in snake_case, as everywhere in Synapsis's JSON.</summary>
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly IElasticClient _elastic;
    private readonly ILogger<IngestLogRepository> _logger;

    /// <summary>True once the index is known to exist, so it is checked only once per process.</summary>
    private bool _indexReady;

    /// <summary>Creates the repository over the Elasticsearch connection; warnings go to the given logger.</summary>
    public IngestLogRepository(IElasticClient elastic, ILogger<IngestLogRepository> logger)
    {
        _elastic = elastic;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddAsync(IReadOnlyList<IngestLogEntry> entries)
    {
        if (!_elastic.Enabled)
        {
            return;
        }
        try
        {
            await EnsureIndexAsync();
            foreach (IngestLogEntry entry in entries)
            {
                await _elastic.SendAsync(HttpMethod.Post, Index + "/_doc", JsonSerializer.SerializeToNode(entry, Json));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
        {
            _logger.LogWarning("The ingestion log could not be written (the records are stored): {Reason}", exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IngestLogEntry>> LatestAsync(DateTime? since, string? source, int limit)
    {
        List<IngestLogEntry> entries = new List<IngestLogEntry>();
        if (!_elastic.Enabled)
        {
            return entries;
        }
        JsonNode? answer = await _elastic.SendAsync(HttpMethod.Post, Index + "/_search?ignore_unavailable=true", LatestQuery(since, source, limit));
        foreach (JsonNode? hit in answer!["hits"]!["hits"]!.AsArray())
        {
            entries.Add(hit!["_source"].Deserialize<IngestLogEntry>(Json)!);
        }
        return entries;
    }

    /// <inheritdoc/>
    public async Task<IngestStats> StatsAsync(DateTime from, DateTime to)
    {
        if (!_elastic.Enabled)
        {
            return new IngestStats { From = from, To = to };
        }
        JsonNode? answer = await _elastic.SendAsync(HttpMethod.Post, Index + "/_search?ignore_unavailable=true", IngestStatsMapper.Query(from, to));
        return IngestStatsMapper.ToStats(from, to, answer!);
    }

    /// <summary>Creates the index with its mapping when it does not exist yet.</summary>
    private async Task EnsureIndexAsync()
    {
        if (_indexReady)
        {
            return;
        }
        if (!await _elastic.ExistsAsync(Index))
        {
            await _elastic.SendAsync(HttpMethod.Put, Index, JsonNode.Parse(Mapping));
        }
        _indexReady = true;
    }

    /// <summary>The newest entries first, filtered by time and source when asked.</summary>
    private static JsonNode LatestQuery(DateTime? since, string? source, int limit)
    {
        JsonArray filters = new JsonArray();
        if (since != null)
        {
            filters.Add(new JsonObject { ["range"] = new JsonObject { ["timestamp"] = new JsonObject { ["gt"] = since.Value.ToString("O") } } });
        }
        if (!string.IsNullOrEmpty(source))
        {
            filters.Add(new JsonObject { ["term"] = new JsonObject { ["source"] = source } });
        }
        return new JsonObject
        {
            ["size"] = limit,
            ["sort"] = new JsonArray(new JsonObject { ["timestamp"] = "desc" }),
            ["query"] = new JsonObject { ["bool"] = new JsonObject { ["filter"] = filters } },
        };
    }

    /// <summary>
    /// The index mapping. Names and sources are keywords (exact values, for filters and totals); the error counts are
    /// "nested" so each code keeps its own count; the detailed rejections are stored but not searchable.
    /// </summary>
    private const string Mapping = """
        {
          "mappings": {
            "properties": {
              "timestamp":   { "type": "date" },
              "batch_id":    { "type": "keyword" },
              "producer":    { "type": "keyword" },
              "source":      { "type": "keyword" },
              "received":    { "type": "long" },
              "new":         { "type": "long" },
              "changed":     { "type": "long" },
              "unchanged":   { "type": "long" },
              "duplicates":  { "type": "long" },
              "rejected":    { "type": "long" },
              "duration_ms": { "type": "long" },
              "rejection_counts": {
                "type": "nested",
                "properties": { "code": { "type": "keyword" }, "count": { "type": "long" } }
              },
              "rejections":  { "type": "object", "enabled": false }
            }
          }
        }
        """;
}
