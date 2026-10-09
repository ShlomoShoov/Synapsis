using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Tests.Support;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Services.EnvelopeRules;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.EnvelopeRules;

/// <summary>Rule 1: the record kind.</summary>
public class KindRuleTests
{
    /// <summary>The rule, through its abstraction.</summary>
    private static readonly IEnvelopeRule Rule = new KindRule();

    /// <summary>A known kind passes.</summary>
    [Fact]
    public void A_known_kind_passes()
    {
        Assert.True(Rule.Check(TestRecords.ToElement(TestRecords.Earthquake())).Succeeded);
    }

    /// <summary>A source sent as a record, or no kind at all, is E-KIND at "kind".</summary>
    [Theory]
    [InlineData("source")]
    [InlineData(null)]
    public void An_unknown_or_missing_kind_is_refused(string? kindText)
    {
        JsonObject record = TestRecords.Earthquake();
        record["kind"] = kindText;
        Result result = Rule.Check(TestRecords.ToElement(record));
        Assert.Equal(ErrorCodes.Kind, result.Problem!.Code);
        Assert.Equal("kind", result.Problem.Path);
    }
}
