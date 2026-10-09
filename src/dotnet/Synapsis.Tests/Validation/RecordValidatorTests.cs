using System;
using System.Text.Json.Nodes;
using Synapsis.Sil;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation;

/// <summary>Checks the whole record validation: the typed result, and the fixed order of the rules.</summary>
public class RecordValidatorTests
{
    /// <summary>The record validator, through its abstraction.</summary>
    private static readonly IRecordValidator Validator = SilComponents.CreateRecordValidator();

    /// <summary>A valid record passes and comes back typed.</summary>
    [Fact]
    public void A_valid_record_passes_and_is_typed()
    {
        Result<SilRecord> result = Validator.Validate(Request(TestRecords.Earthquake()));
        Assert.True(result.Succeeded);
        Assert.IsType<EventRecord>(result.Value);
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
        Result<SilRecord> result = Validator.Validate(Request(record));
        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
    }

    /// <summary>A request for one record, with the real registry.</summary>
    private static RecordValidationRequest Request(JsonObject record)
    {
        return new RecordValidationRequest(TestRecords.ToElement(record), TestRegistry.All, DateTime.UtcNow);
    }
}
