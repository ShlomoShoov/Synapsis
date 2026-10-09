using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 3: the source must be declared in a schema package, and the reliability basis and the known_at basis must be
/// in core's closed lists. Every later rule relies on the source (rule 4 needs its package).
/// </summary>
public sealed class SourceRule : IRecordRule
{
    /// <summary>Passes, or fails with E-REGISTRY-SOURCE, or E-SCHEMA for an unknown basis.</summary>
    public Result Check(RecordContext context)
    {
        SilRecord record = context.Record;
        IRegistry registry = context.Registry;
        if (registry.FindSource(record.Source) == null)
        {
            return Result.Failure(ErrorCodes.RegistrySource, "source", $"source '{record.Source}' is not registered");
        }
        if (!registry.IsReliabilityBasis(record.ReliabilityBasis))
        {
            return Result.Failure(ErrorCodes.Schema, "reliability_basis", $"unknown reliability basis '{record.ReliabilityBasis}'");
        }
        if (!registry.IsKnownAtBasis(record.KnownAtBasis))
        {
            return Result.Failure(ErrorCodes.Schema, "known_at_basis", $"unknown known_at basis '{record.KnownAtBasis}'");
        }
        return Result.Success();
    }
}
