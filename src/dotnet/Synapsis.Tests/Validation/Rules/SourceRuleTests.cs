using System.Text.Json.Nodes;
using Synapsis.Sil.Validation;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.Rules;

/// <summary>Rule 3: the source and the two bases.</summary>
public class SourceRuleTests
{
    /// <summary>An undeclared source is E-REGISTRY-SOURCE.</summary>
    [Fact]
    public void An_undeclared_source_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["source"] = "src:nobody:nothing";
        RuleCheck.Refused(record, ErrorCodes.RegistrySource, "source");
    }

    /// <summary>A reliability basis outside core's list is E-SCHEMA at reliability_basis.</summary>
    [Fact]
    public void An_unknown_reliability_basis_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["reliability_basis"] = "Z9";
        RuleCheck.Refused(record, ErrorCodes.Schema, "reliability_basis");
    }

    /// <summary>A known_at basis outside core's list is E-SCHEMA at known_at_basis.</summary>
    [Fact]
    public void An_unknown_known_at_basis_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at_basis"] = "rumour";
        RuleCheck.Refused(record, ErrorCodes.Schema, "known_at_basis");
    }
}
