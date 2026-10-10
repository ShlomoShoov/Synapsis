using System;
using System.Collections.Generic;
using Synapsis.Sil.Models.Versioning;

namespace Synapsis.Sil.Services.Versioning;

/// <summary>
/// Questions about the stored versions of one statement: the latest one, the one current at a moment, and whether a
/// re-read of the past repeats what was known then. Used by <see cref="VersionPlanner"/>.
/// </summary>
public static class VersionHistory
{
    /// <summary>The version with the highest number, or null when there is none.</summary>
    public static StoredVersion? Latest(IReadOnlyList<StoredVersion> versions)
    {
        StoredVersion? latest = null;
        foreach (StoredVersion version in versions)
        {
            if (latest == null || version.Version > latest.Version)
            {
                latest = version;
            }
        }
        return latest;
    }

    /// <summary>True when the version current at <paramref name="at"/> has this content (rule 3).</summary>
    public static bool RepeatsWhatWasKnownThen(IReadOnlyList<StoredVersion> versions, DateTime at, string fingerprint)
    {
        StoredVersion? then = CurrentAt(versions, at);
        return then != null && then.Fingerprint == fingerprint;
    }

    /// <summary>
    /// The version that was the latest one known at <paramref name="at"/>: the greatest known_at not after it, and of
    /// those the highest version. Null when nothing was known yet.
    /// </summary>
    public static StoredVersion? CurrentAt(IReadOnlyList<StoredVersion> versions, DateTime at)
    {
        StoredVersion? best = null;
        foreach (StoredVersion version in versions)
        {
            if (version.KnownAt > at)
            {
                continue;
            }
            if (best == null || IsLater(version, best))
            {
                best = version;
            }
        }
        return best;
    }

    /// <summary>True when the first version was known later than the second, or at the same time with a higher number.</summary>
    public static bool IsLater(StoredVersion first, StoredVersion second)
    {
        if (first.KnownAt != second.KnownAt)
        {
            return first.KnownAt > second.KnownAt;
        }
        return first.Version > second.Version;
    }
}
