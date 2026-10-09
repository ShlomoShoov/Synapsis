using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Synapsis.Sil.Services.Time;
using Synapsis.Sil.Validation.Models.Domain;
using Synapsis.Sil.Versioning.Abstractions;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Sil.Versioning.Models.Requests;
using Synapsis.Sil.Versioning.Models.Results;

namespace Synapsis.Sil.Versioning.Services.Domain;

/// <summary>
/// The four version rules, per statement (record id + source):
/// 1. Nothing stored: version 1, known when the record says.
/// 2. The same content as the latest version: unchanged, whatever its known_at (a stored past is never rewritten).
/// 3. A known_at earlier than the latest version's is a re-read of the past (a backfill run again). It is compared
///    with the version that was current at that time: the same content is unchanged, because the source only repeats
///    what was already known then.
/// 4. Otherwise the content changed: the next version. It is known when the record says if that is after the latest
///    version and not after the receive time; otherwise it is known at the receive time, with basis "ingested",
///    because what Synapsis learns now cannot be placed in its past.
/// Rule 3 keeps a partial re-run honest: re-reading 2025 after 2026 is loaded must not make a bank's 2025 name its
/// latest version again.
/// </summary>
public sealed class VersionPlanner : IVersionPlanner
{
    /// <summary>The known_at basis of a version whose knowledge time became the receive time.</summary>
    private const string IngestedBasis = "ingested";

    /// <inheritdoc/>
    public VersionPlan Plan(VersionPlanRequest request)
    {
        CheckedRecord record = request.Record;
        DateTime knownAt = KnownAtOf(record);
        StoredVersion? latest = Latest(request.StoredVersions);
        if (latest == null)
        {
            return VersionPlan.New(knownAt, record.Raw);
        }
        if (latest.Fingerprint == record.Fingerprint)
        {
            return VersionPlan.Unchanged();
        }
        if (knownAt < latest.KnownAt && RepeatsWhatWasKnownThen(request.StoredVersions, knownAt, record.Fingerprint))
        {
            return VersionPlan.Unchanged();
        }
        int next = latest.Version + 1;
        if (latest.KnownAt < knownAt && knownAt <= request.ReceivedAt)
        {
            return VersionPlan.NewVersion(next, knownAt, record.Raw);
        }
        return VersionPlan.NewVersion(next, request.ReceivedAt, AsLearnedNow(record.Raw, request.ReceivedAt));
    }

    /// <summary>The record's known_at. It passed validation, so it is a valid instant.</summary>
    private static DateTime KnownAtOf(CheckedRecord record)
    {
        if (!Instant.TryParse(record.Record.KnownAt, out DateTime knownAt))
        {
            throw new InvalidOperationException($"Record {record.Record.Id} reached version planning with an invalid known_at.");
        }
        return knownAt;
    }

    /// <summary>The version with the highest number, or null when there is none.</summary>
    private static StoredVersion? Latest(IReadOnlyList<StoredVersion> versions)
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
    private static bool RepeatsWhatWasKnownThen(IReadOnlyList<StoredVersion> versions, DateTime at, string fingerprint)
    {
        StoredVersion? then = CurrentAt(versions, at);
        return then != null && then.Fingerprint == fingerprint;
    }

    /// <summary>
    /// The version that was the latest one known at <paramref name="at"/>: the greatest known_at not after it, and of
    /// those the highest version. Null when nothing was known yet.
    /// </summary>
    private static StoredVersion? CurrentAt(IReadOnlyList<StoredVersion> versions, DateTime at)
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
    private static bool IsLater(StoredVersion first, StoredVersion second)
    {
        if (first.KnownAt != second.KnownAt)
        {
            return first.KnownAt > second.KnownAt;
        }
        return first.Version > second.Version;
    }

    /// <summary>The record with known_at set to the receive time and known_at_basis set to "ingested" (rule 4).</summary>
    private static JsonElement AsLearnedNow(JsonElement record, DateTime receivedAt)
    {
        JsonObject copy = JsonNode.Parse(record.GetRawText())!.AsObject();
        copy["known_at"] = Instant.Format(receivedAt);
        copy["known_at_basis"] = IngestedBasis;
        return JsonSerializer.SerializeToElement(copy);
    }
}
