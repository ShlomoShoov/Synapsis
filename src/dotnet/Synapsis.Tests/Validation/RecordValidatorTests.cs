using System;
using System.Text.Json.Nodes;
using Synapsis.Sil.Records;
using Synapsis.Sil.Validation;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation;

/// <summary>Checks the whole record check: the typed result, and the fixed order of the rules.</summary>
public class RecordValidatorTests
{
    /// <summary>A valid record passes and comes back typed.</summary>
    [Fact]
    public void A_valid_record_passes_and_is_typed()
    {
        RecordError? error = RecordValidator.Check(TestRecords.ToElement(TestRecords.Earthquake()), TestRegistry.All,
            DateTime.UtcNow, out SilRecord? record);
        Assert.Null(error);
        Assert.IsType<EventRecord>(record);
    }

    /// <summary>A record breaking two rules gets the earlier one's code: the source (rule 3) before the time (rule 6).</summary>
    [Fact]
    public void The_earlier_rule_decides()
    {
        JsonObject record = TestRecords.Earthquake();
        record["source"] = "src:nobody:nothing";
        record["time"]!["precision"] = "day";
        RuleCheck.Refused(record, ErrorCodes.RegistrySource, "source");
    }

    /// <summary>A refused record does not come back typed.</summary>
    [Fact]
    public void A_refused_record_is_not_returned()
    {
        JsonObject record = TestRecords.Earthquake();
        record["type"] = "GEO.METEOR";
        RecordValidator.Check(TestRecords.ToElement(record), TestRegistry.All, DateTime.UtcNow, out SilRecord? typed);
        Assert.Null(typed);
    }
}
