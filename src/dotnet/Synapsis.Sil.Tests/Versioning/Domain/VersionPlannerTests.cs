using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Services.Time;
using Synapsis.Sil.Tests.Support;
using Synapsis.Sil.Validation.Models.Domain;
using Synapsis.Sil.Versioning.Abstractions;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Sil.Versioning.Models.Requests;
using Synapsis.Sil.Versioning.Models.Results;
using Xunit;

namespace Synapsis.Sil.Tests.Versioning.Domain;

/// <summary>
/// The four version rules, with the same cases as the stand-in's test_versions.py. Content is given as a short
/// fingerprint ("A", "B") so each case reads like its story.
/// </summary>
public class VersionPlannerTests
{
    /// <summary>The planner, through its abstraction.</summary>
    private static readonly IVersionPlanner Planner = SilComponents.CreateVersionPlanner();

    /// <summary>The receive time of every case.</summary>
    private static readonly DateTime Now = Day(2026, 10, 8);

    /// <summary>Rule 1: the first statement is version 1, known when the record says.</summary>
    [Fact]
    public void The_first_statement_is_version_one_known_as_sent()
    {
        VersionPlan plan = Plan(Stored(), "A", Day(2006, 5, 15));
        Assert.Equal(VersionOutcome.New, plan.Outcome);
        Assert.Equal(1, plan.Version);
        Assert.Equal(Day(2006, 5, 15), plan.KnownAt);
    }

    /// <summary>Rule 2: the same content as the latest version is unchanged, even with an earlier known_at.</summary>
    [Fact]
    public void The_same_content_as_the_latest_is_unchanged_even_with_an_earlier_known_at()
    {
        VersionPlan plan = Plan(Stored(("A", Day(2025, 5, 16))), "A", Day(2006, 5, 15));
        Assert.Equal(VersionOutcome.Unchanged, plan.Outcome);
        Assert.Null(plan.Version);
    }

    /// <summary>Rule 4: a change known later becomes the next version, known when the record says.</summary>
    [Fact]
    public void A_change_known_later_becomes_the_next_version_known_as_sent()
    {
        VersionPlan plan = Plan(Stored(("A", Day(2006, 5, 15))), "B", Day(2012, 8, 14));
        Assert.Equal(VersionOutcome.NewVersion, plan.Outcome);
        Assert.Equal(2, plan.Version);
        Assert.Equal(Day(2012, 8, 14), plan.KnownAt);
        Assert.Equal("2012-08-14T00:00:00Z", KnownAtStored(plan));
    }

    /// <summary>
    /// Rule 3: a bank named A from 2006 was renamed B in 2026; a re-run of 2025 sends A again with its 2025 known_at.
    /// That repeats what was known in 2025, so it is unchanged.
    /// </summary>
    [Fact]
    public void Re_reading_the_past_repeats_what_was_known_then()
    {
        List<StoredVersion> stored = Stored(("A", Day(2006, 5, 15)), ("B", Day(2026, 5, 15)));
        Assert.Equal(VersionOutcome.Unchanged, Plan(stored, "A", Day(2025, 5, 16)).Outcome);
    }

    /// <summary>
    /// A 2025 figure restated: it differs from the version current in 2025, so it is new knowledge, stored as version 3
    /// and known now, with basis "ingested".
    /// </summary>
    [Fact]
    public void A_past_that_differs_from_what_was_known_then_is_stored_as_learned_now()
    {
        List<StoredVersion> stored = Stored(("A", Day(2006, 5, 15)), ("B", Day(2026, 5, 15)));
        VersionPlan plan = Plan(stored, "C", Day(2025, 5, 16));
        Assert.Equal(VersionOutcome.NewVersion, plan.Outcome);
        Assert.Equal(3, plan.Version);
        Assert.Equal(Now, plan.KnownAt);
        Assert.Equal("2026-10-08T00:00:00Z", KnownAtStored(plan));
        Assert.Equal("ingested", plan.RecordToStore!.Value.GetProperty("known_at_basis").GetString());
    }

    /// <summary>2025 loaded first, then the 2006 backfill: nothing was current in 2006, so the 2006 name is learned now.</summary>
    [Fact]
    public void Older_information_loaded_after_newer_is_known_now()
    {
        VersionPlan plan = Plan(Stored(("B", Day(2025, 5, 16))), "A", Day(2006, 5, 15));
        Assert.Equal(VersionOutcome.NewVersion, plan.Outcome);
        Assert.Equal(2, plan.Version);
        Assert.Equal(Now, plan.KnownAt);
    }

    /// <summary>A known_at after the receive time is replaced by the receive time.</summary>
    [Fact]
    public void A_known_at_after_the_receive_time_becomes_the_receive_time()
    {
        VersionPlan plan = Plan(Stored(("A", Day(2006, 5, 15))), "B", Day(2027, 1, 1));
        Assert.Equal(Now, plan.KnownAt);
        Assert.Equal("ingested", plan.RecordToStore!.Value.GetProperty("known_at_basis").GetString());
    }

    /// <summary>The version compared in rule 3 is the latest one known by then: A until 2012, B from 2012, none before 2006.</summary>
    [Fact]
    public void The_version_current_then_is_the_latest_known_by_then()
    {
        List<StoredVersion> stored = Stored(("A", Day(2006, 1, 1)), ("B", Day(2012, 1, 1)), ("C", Day(2020, 1, 1)));
        Assert.Equal(VersionOutcome.Unchanged, Plan(stored, "A", Day(2011, 1, 1)).Outcome);
        Assert.Equal(VersionOutcome.Unchanged, Plan(stored, "B", Day(2012, 1, 1)).Outcome);
        Assert.Equal(VersionOutcome.NewVersion, Plan(stored, "A", Day(2005, 1, 1)).Outcome);
    }

    /// <summary>Plans the earthquake record with this content and this known_at.</summary>
    private static VersionPlan Plan(List<StoredVersion> stored, string fingerprint, DateTime knownAt)
    {
        return Planner.Plan(new VersionPlanRequest(Checked(fingerprint, knownAt), stored, Now));
    }

    /// <summary>Stored versions 1, 2, ... from (content, known_at) pairs, oldest first.</summary>
    private static List<StoredVersion> Stored(params (string Fingerprint, DateTime KnownAt)[] versions)
    {
        List<StoredVersion> stored = new List<StoredVersion>();
        for (int index = 0; index < versions.Length; index++)
        {
            stored.Add(new StoredVersion { Version = index + 1, Fingerprint = versions[index].Fingerprint, KnownAt = versions[index].KnownAt });
        }
        return stored;
    }

    /// <summary>A checked earthquake with this fingerprint and known_at.</summary>
    private static CheckedRecord Checked(string fingerprint, DateTime knownAt)
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = Instant.Format(knownAt);
        JsonElement raw = TestRecords.ToElement(record);
        SilRecord typed = SilComponents.CreateRecordReader().Read(raw).Value!;
        return new CheckedRecord(0, raw, typed, fingerprint);
    }

    /// <summary>The known_at of the record the plan would store.</summary>
    private static string? KnownAtStored(VersionPlan plan)
    {
        return plan.RecordToStore!.Value.GetProperty("known_at").GetString();
    }

    /// <summary>Midnight UTC of a day.</summary>
    private static DateTime Day(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}
