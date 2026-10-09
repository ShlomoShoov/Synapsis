using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Tests.Support;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Services.EnvelopeRules;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.EnvelopeRules;

/// <summary>Rule 2: the record's structure against the record language's JSON Schema.</summary>
public class StructureRuleTests
{
    /// <summary>The rule, through its abstraction.</summary>
    private static readonly IEnvelopeRule Rule = new StructureRule();

    /// <summary>A valid record passes.</summary>
    [Fact]
    public void A_valid_record_passes()
    {
        Assert.True(Rule.Check(TestRecords.ToElement(TestRecords.Earthquake())).Succeeded);
    }

    /// <summary>A field the record language does not have is E-SCHEMA.</summary>
    [Fact]
    public void An_undeclared_field_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["severity"] = 3;
        Assert.Equal(ErrorCodes.Schema, Fail(record).Code);
    }

    /// <summary>A missing required field is E-SCHEMA, located at the record itself (the empty path).</summary>
    [Fact]
    public void A_missing_required_field_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record.Remove("known_at");
        Problem problem = Fail(record);
        Assert.Equal(ErrorCodes.Schema, problem.Code);
        Assert.Equal(string.Empty, problem.Path);
    }

    /// <summary>A value of the wrong type or out of range is E-SCHEMA, located at the value.</summary>
    [Fact]
    public void A_wrong_value_is_refused_at_its_location()
    {
        JsonObject record = TestRecords.Earthquake();
        record["reliability"] = 101;
        Problem problem = Fail(record);
        Assert.Equal(ErrorCodes.Schema, problem.Code);
        Assert.Equal("reliability", problem.Path);
    }

    /// <summary>An instant with an offset instead of Z is E-SCHEMA.</summary>
    [Fact]
    public void An_instant_with_an_offset_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = "2024-01-01T08:10:09+00:00";
        Assert.Equal(ErrorCodes.Schema, Fail(record).Code);
    }

    /// <summary>Runs the rule and expects a failure.</summary>
    private static Problem Fail(JsonObject record)
    {
        Result result = Rule.Check(TestRecords.ToElement(record));
        Assert.False(result.Succeeded);
        return result.Problem!;
    }
}
