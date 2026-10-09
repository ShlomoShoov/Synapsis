using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 11: the attributes.</summary>
public class AttributeRuleTests
{
    /// <summary>An attribute the type does not declare is E-ATTRIBUTE at its name.</summary>
    [Fact]
    public void An_undeclared_attribute_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["attributes"] = new JsonObject { ["felt_by"] = "many" };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.felt_by");
    }

    /// <summary>A value of the wrong type is E-ATTRIBUTE.</summary>
    [Fact]
    public void A_value_of_the_wrong_type_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["attributes"] = new JsonObject { ["magnitude_type"] = 5 };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.magnitude_type");
    }

    /// <summary>A text longer than allowed is E-ATTRIBUTE.</summary>
    [Fact]
    public void A_text_too_long_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["attributes"] = new JsonObject { ["magnitude_type"] = "far-too-long-a-type" };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.magnitude_type");
    }

    /// <summary>A list item that breaks the pattern (KEV's CWE ids) is E-ATTRIBUTE.</summary>
    [Fact]
    public void A_pattern_mismatch_is_refused()
    {
        JsonObject record = TestRecords.KevEvent();
        record["attributes"] = new JsonObject { ["cwes"] = new JsonArray("89") };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.cwes");
    }

    /// <summary>A value outside the allowed list is E-ATTRIBUTE.</summary>
    [Fact]
    public void A_value_outside_the_allowed_list_is_refused()
    {
        JsonObject record = TestRecords.KevEvent();
        record["attributes"] = new JsonObject { ["known_ransomware_campaign_use"] = "Maybe" };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.known_ransomware_campaign_use");
    }

    /// <summary>A date that does not exist is E-ATTRIBUTE.</summary>
    [Fact]
    public void An_impossible_date_is_refused()
    {
        JsonObject record = TestRecords.KevEvent();
        record["attributes"] = new JsonObject { ["due_date"] = "2024-02-30" };
        RuleCheck.Refused(record, ErrorCodes.Attribute, "attributes.due_date");
    }
}
