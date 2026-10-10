using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// Writes the ingestion log to the Elasticsearch index "synapsis-ingest-log": one document per batch and source. The UI
/// reads the index directly (the document's fields are in contracts/synapsis-api.yaml).
/// </summary>
public interface IIngestLogRepository
{
    /// <summary>
    /// Adds the entries of one batch. Best effort: if Elasticsearch fails, a warning is logged and the call still returns
    /// normally, because the log must never stop data from being stored. Does nothing when Elasticsearch is off.
    /// </summary>
    Task AddAsync(IReadOnlyList<IngestLogEntry> entries);
}
