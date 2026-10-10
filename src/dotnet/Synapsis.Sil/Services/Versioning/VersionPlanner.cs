using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Synapsis.Sil.Services.Time;
using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Models.Versioning;
namespace Synapsis.Sil.Services.Versioning;

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
        StoredVersion? latest = VersionHistory.Latest(request.StoredVersions);
        if (latest == null)
        {
            return VersionPlan.New(knownAt, record.Raw);
        }
        if (latest.Fingerprint == record.Fingerprint)
        {
            return VersionPlan.Unchanged();
        }
        if (knownAt < latest.KnownAt && VersionHistory.RepeatsWhatWasKnownThen(request.StoredVersions, knownAt, record.Fingerprint))
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

    /// <inheritdoc/>
    public IReadOnlyList<StatementKey> StatementsOf(IReadOnlyList<CheckedRecord> accepted)
    {
        List<StatementKey> keys = new List<StatementKey>();
        foreach (CheckedRecord record in accepted)
        {
            keys.Add(StatementOf(record));
        }
        return keys;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PlannedRecord> PlanBatch(IReadOnlyList<CheckedRecord> accepted, IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored, DateTime receivedAt)
    {
        List<PlannedRecord> planned = new List<PlannedRecord>();
        foreach (CheckedRecord record in accepted)
        {
            IReadOnlyList<StoredVersion> versions = StoredOf(stored, StatementOf(record));
            planned.Add(new PlannedRecord(record, Plan(new VersionPlanRequest(record, versions, receivedAt))));
        }
        return planned;
    }

    /// <summary>The statement a record belongs to: its id from its source.</summary>
    private static StatementKey StatementOf(CheckedRecord record)
    {
        return new StatementKey { Id = record.Record.Id, Source = record.Record.Source };
    }

    /// <summary>The stored versions of one statement; none when it is new.</summary>
    private static IReadOnlyList<StoredVersion> StoredOf(IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored, StatementKey key)
    {
        if (stored.TryGetValue(key, out IReadOnlyList<StoredVersion>? versions))
        {
            return versions;
        }
        return Array.Empty<StoredVersion>();
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

    /// <summary>The record with known_at set to the receive time and known_at_basis set to "ingested" (rule 4).</summary>
    private static JsonElement AsLearnedNow(JsonElement record, DateTime receivedAt)
    {
        JsonObject copy = JsonNode.Parse(record.GetRawText())!.AsObject();
        copy["known_at"] = Instant.Format(receivedAt);
        copy["known_at_basis"] = IngestedBasis;
        return JsonSerializer.SerializeToElement(copy);
    }
}
