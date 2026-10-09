using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Results;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.RecordRules;

/// <summary>Rule 12: conclusions and rule A-CLOSED.</summary>
public class ConclusionRuleTests
{
    /// <summary>A producer kind outside core's list is E-SCHEMA at method.by.</summary>
    [Fact]
    public void An_unknown_producer_kind_is_refused()
    {
        JsonObject record = TestRecords.Classification();
        record["method"]!["by"] = "oracle";
        RuleCheck.Refused(record, ErrorCodes.Schema, "method.by");
    }

    /// <summary>A class outside the closed space is E-LOGICAL-VALUE (a model cannot invent a class).</summary>
    [Fact]
    public void A_class_outside_the_closed_space_is_refused()
    {
        JsonObject record = TestRecords.Classification();
        record["statement"]!["object"] = "cyber.product_class/TOASTER";
        RuleCheck.Refused(record, ErrorCodes.LogicalValue, "statement.object");
    }

    /// <summary>An unknown space is E-LOGICAL-SPACE; a classification with no class is E-LOGICAL-DEPTH.</summary>
    [Fact]
    public void Wrong_classification_targets_are_refused()
    {
        JsonObject unknownSpace = TestRecords.Classification();
        unknownSpace["statement"]!["object"] = "cyber.nowhere/MFT";
        RuleCheck.Refused(unknownSpace, ErrorCodes.LogicalSpace, "statement.object");

        JsonObject noClass = TestRecords.Classification();
        noClass["statement"]!["object"] = "cyber.product_class";
        RuleCheck.Refused(noClass, ErrorCodes.LogicalDepth, "statement.object");
    }
}
