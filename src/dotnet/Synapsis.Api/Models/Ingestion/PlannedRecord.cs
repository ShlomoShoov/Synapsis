using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Models.Versioning;
namespace Synapsis.Api.Models.Ingestion;

/// <summary>An accepted record and the version planner's decision for it.</summary>
public sealed class PlannedRecord
{
    /// <summary>Creates a planned record.</summary>
    public PlannedRecord(CheckedRecord record, VersionPlan plan)
    {
        Record = record;
        Plan = plan;
    }

    /// <summary>The accepted record.</summary>
    public CheckedRecord Record { get; }

    /// <summary>New, unchanged, or the next version.</summary>
    public VersionPlan Plan { get; }
}
