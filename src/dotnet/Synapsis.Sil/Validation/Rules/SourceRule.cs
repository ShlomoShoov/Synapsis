using Synapsis.Sil.Records;
using Synapsis.Sil.Registry;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 3: the source must be declared in a schema package, and the reliability basis and the known_at basis must be
/// in core's closed lists. Every later rule relies on the source (rule 4 needs its package).
/// </summary>
public sealed class SourceRule : IRecordRule
{
    /// <summary>Returns E-REGISTRY-SOURCE, or E-SCHEMA for an unknown basis, or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        SilRecord record = context.Record;
        RegistrySnapshot registry = context.Registry;
        if (!registry.Sources.ContainsKey(record.Source))
        {
            return new RecordError(ErrorCodes.RegistrySource, "source", $"source '{record.Source}' is not registered");
        }
        if (!registry.ReliabilityBases.Contains(record.ReliabilityBasis))
        {
            return new RecordError(ErrorCodes.Schema, "reliability_basis",
                $"unknown reliability basis '{record.ReliabilityBasis}'");
        }
        if (!registry.KnownAtBases.Contains(record.KnownAtBasis))
        {
            return new RecordError(ErrorCodes.Schema, "known_at_basis", $"unknown known_at basis '{record.KnownAtBasis}'");
        }
        return null;
    }
}
