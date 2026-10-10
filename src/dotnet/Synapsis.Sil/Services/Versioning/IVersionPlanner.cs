using System;
using System.Collections.Generic;
using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Models.Versioning;
namespace Synapsis.Sil.Services.Versioning;

/// <summary>
/// Decides what an accepted record means for the store: a new statement (version 1), nothing new (unchanged), or the
/// next version of what is stored. A statement is one record id from one source; its versions are never rewritten.
/// The decision depends only on content (the fingerprint) and on when each version became known.
/// </summary>
public interface IVersionPlanner
{
    /// <summary>The plan for one accepted record, given the versions already stored for its statement.</summary>
    VersionPlan Plan(VersionPlanRequest request);

    /// <summary>The statements (id and source) of the accepted records: what to ask the store for before planning.</summary>
    IReadOnlyList<StatementKey> StatementsOf(IReadOnlyList<CheckedRecord> accepted);

    /// <summary>
    /// The plan for every accepted record of a batch. <paramref name="stored"/> holds the stored versions of each
    /// statement; a statement missing from it is new.
    /// </summary>
    IReadOnlyList<PlannedRecord> PlanBatch(IReadOnlyList<CheckedRecord> accepted, IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored, DateTime receivedAt);
}
