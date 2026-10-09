using System.Threading.Tasks;
using Synapsis.Sil.Validation.Models;

namespace Synapsis.Sil.Validation.Abstractions;

/// <summary>
/// Validates a whole batch of raw records: which may enter Synapsis, which are refused (each with its problem), and
/// how many were identical copies of an earlier record in the batch.
/// </summary>
public interface IBatchValidator
{
    /// <summary>Validates the batch against the request's vocabulary and the records already stored.</summary>
    Task<BatchValidationResult> ValidateAsync(BatchValidationRequest request);
}
