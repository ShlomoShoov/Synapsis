using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 10: the text policy of event types.</summary>
public class TextRuleTests
{
    /// <summary>Text on a type that carries none (an earthquake) is E-TEXT.</summary>
    [Fact]
    public void Text_on_a_type_without_text_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["text"] = new JsonObject { ["title"] = "A strong earthquake" };
        RuleCheck.Refused(record, ErrorCodes.Text, "text");
    }

    /// <summary>No text on a type that requires it (a KEV entry) is E-TEXT.</summary>
    [Fact]
    public void Missing_required_text_is_refused()
    {
        JsonObject record = TestRecords.KevEvent();
        record.Remove("text");
        RuleCheck.Refused(record, ErrorCodes.Text, "text");
    }
}
