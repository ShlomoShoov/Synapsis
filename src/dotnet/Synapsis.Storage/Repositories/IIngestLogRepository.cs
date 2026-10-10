using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// The ingestion log in Elasticsearch (index "synapsis-ingest-log"): one entry per batch and source. The UI reads it to
/// show the live flow of data and its statistics. When Elasticsearch is off, writing does nothing and reading finds
/// nothing.
/// </summary>
public interface IIngestLogRepository
{
    /// <summary>
    /// Adds the entries of one batch. Best effort: if Elasticsearch fails, the failure is logged as a warning and the
    /// call still returns normally, because the log must never stop data from being stored.
    /// </summary>
    Task AddAsync(IReadOnlyList<IngestLogEntry> entries);

    /// <summary>The newest entries, newest first: only those after <paramref name="since"/> and of <paramref name="source"/> when given.</summary>
    Task<IReadOnlyList<IngestLogEntry>> LatestAsync(DateTime? since, string? source, int limit);

    /// <summary>The totals between two moments: per source, and refusals per error code.</summary>
    Task<IngestStats> StatsAsync(DateTime from, DateTime to);
}
