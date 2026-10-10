using System.Threading.Tasks;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Batch;

/// <summary>
/// A rule about records across the batch, checked after each record passed on its own (rule 13: duplicates,
/// references). It receives the records still in the running and answers which pass on.
/// </summary>
public interface IBatchRule
{
    /// <summary>The records that pass, the ones refused, and how many were dropped as identical copies.</summary>
    Task<BatchRuleResult> CheckAsync(BatchRuleRequest request);
}
