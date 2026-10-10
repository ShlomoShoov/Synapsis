using System.Threading.Tasks;
using Synapsis.Api.Models.Ingestion;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Orchestrators;

/// <summary>
/// Ingests one batch: reads it, validates its records, decides each accepted record's version, keeps the new versions
/// and answers a receipt. A dry run does everything but keep.
/// </summary>
public interface IIngestionOrchestrator
{
    /// <summary>
    /// The receipt; or E-BUNDLE / E-TOO-LARGE when the body is not a batch, E-UNAVAILABLE when another writer stored
    /// one of the versions first (send again).
    /// </summary>
    Task<Result<Receipt>> IngestAsync(IngestionRequest request);
}
