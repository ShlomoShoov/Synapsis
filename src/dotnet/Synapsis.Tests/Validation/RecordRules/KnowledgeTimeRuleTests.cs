using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Results;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.RecordRules;

/// <summary>Rule 7: the knowledge time.</summary>
public class KnowledgeTimeRuleTests
{
    /// <summary>Known before it happened is E-KNOWN-BEFORE-OCCURRENCE.</summary>
    [Fact]
    public void Known_before_it_happened_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = "2023-12-31T00:00:00Z";
        RuleCheck.Refused(record, ErrorCodes.KnownBeforeOccurrence, "known_at");
    }

    /// <summary>Known more than 5 minutes after now is E-KNOWN-IN-FUTURE.</summary>
    [Fact]
    public void Known_in_the_future_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = Format(DateTime.UtcNow.AddHours(1));
        RuleCheck.Refused(record, ErrorCodes.KnownInFuture, "known_at");
    }

    /// <summary>A producer's clock may run a little ahead: one minute is accepted.</summary>
    [Fact]
    public void A_clock_slightly_ahead_is_accepted()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = Format(DateTime.UtcNow.AddMinutes(1));
        Assert.Null(RuleCheck.ErrorOf(record));
    }

    /// <summary>A known_at that does not exist on the calendar is E-SCHEMA at known_at.</summary>
    [Fact]
    public void An_impossible_known_at_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = "2024-02-30T00:00:00Z";
        RuleCheck.Refused(record, ErrorCodes.Schema, "known_at");
    }

    /// <summary>A time in the SIL format.</summary>
    private static string Format(DateTime time)
    {
        return time.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
