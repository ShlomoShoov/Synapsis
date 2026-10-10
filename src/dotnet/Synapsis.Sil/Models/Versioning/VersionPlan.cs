using System;
using System.Text.Json;

namespace Synapsis.Sil.Models.Versioning;

/// <summary>
/// The planner's decision for one record: the outcome and, when something is to be stored, the version number, when
/// it became known, and the record exactly as it will be stored.
/// </summary>
public sealed class VersionPlan
{
    /// <summary>Creates a plan; use the named constructors below.</summary>
    private VersionPlan(VersionOutcome outcome, int? version, DateTime? knownAt, JsonElement? recordToStore)
    {
        Outcome = outcome;
        Version = version;
        KnownAt = knownAt;
        RecordToStore = recordToStore;
    }

    /// <summary>New, unchanged, or a new version.</summary>
    public VersionOutcome Outcome { get; }

    /// <summary>The version to store (1 for a new statement); null when unchanged.</summary>
    public int? Version { get; }

    /// <summary>When the version to store became known (UTC); null when unchanged.</summary>
    public DateTime? KnownAt { get; }

    /// <summary>
    /// The record as it will be stored; null when unchanged. It is the record as sent, except when its known_at had to
    /// become the receive time (then known_at is that time and known_at_basis is "ingested").
    /// </summary>
    public JsonElement? RecordToStore { get; }

    /// <summary>True when something is to be stored.</summary>
    public bool StoresSomething
    {
        get { return Outcome != VersionOutcome.Unchanged; }
    }

    /// <summary>A plan that stores the record as version 1.</summary>
    public static VersionPlan New(DateTime knownAt, JsonElement record)
    {
        return new VersionPlan(VersionOutcome.New, 1, knownAt, record);
    }

    /// <summary>A plan that stores nothing.</summary>
    public static VersionPlan Unchanged()
    {
        return new VersionPlan(VersionOutcome.Unchanged, null, null, null);
    }

    /// <summary>A plan that stores the record as the given version.</summary>
    public static VersionPlan NewVersion(int version, DateTime knownAt, JsonElement record)
    {
        return new VersionPlan(VersionOutcome.NewVersion, version, knownAt, record);
    }
}
