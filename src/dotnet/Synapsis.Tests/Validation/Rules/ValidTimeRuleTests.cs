using System.Text.Json.Nodes;
using Synapsis.Sil.Validation;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.Rules;

/// <summary>Rule 6: the valid time.</summary>
public class ValidTimeRuleTests
{
    /// <summary>A start that does not exist on the calendar is E-SCHEMA at time.start.</summary>
    [Fact]
    public void An_impossible_start_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["time"]!["start"] = "2023-02-30T00:00:00Z";
        RuleCheck.Refused(record, ErrorCodes.Schema, "time.start");
    }

    /// <summary>A "day" that does not start at midnight is E-TIME-ALIGN.</summary>
    [Fact]
    public void A_start_inside_its_period_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["time"]!["precision"] = "day";
        RuleCheck.Refused(record, ErrorCodes.TimeAlign, "time.start");
    }

    /// <summary>A precision outside core's list is E-TIME-ALIGN.</summary>
    [Fact]
    public void An_unknown_precision_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["time"]!["precision"] = "decade";
        RuleCheck.Refused(record, ErrorCodes.TimeAlign, "time.start");
    }

    /// <summary>An end that is not after the start is E-TIME-ORDER.</summary>
    [Fact]
    public void An_end_before_the_start_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["time"]!["end"] = "2023-12-31T00:00:00Z";
        RuleCheck.Refused(record, ErrorCodes.TimeOrder, "time.end");
    }
}
