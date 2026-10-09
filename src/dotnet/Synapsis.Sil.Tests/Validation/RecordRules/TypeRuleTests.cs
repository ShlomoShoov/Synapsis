using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 5: the type is registered for the kind.</summary>
public class TypeRuleTests
{
    /// <summary>An unregistered event type is E-REGISTRY-EVENT-TYPE.</summary>
    [Fact]
    public void An_unknown_event_type_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["type"] = "GEO.METEOR";
        RuleCheck.Refused(record, ErrorCodes.RegistryEventType, "type");
    }

    /// <summary>An event type used for an entity is E-REGISTRY-ENTITY-TYPE.</summary>
    [Fact]
    public void A_type_of_another_kind_is_refused()
    {
        JsonObject record = TestRecords.ValidCase(1)[1];
        record["type"] = "GEO.EARTHQUAKE";
        RuleCheck.Refused(record, ErrorCodes.RegistryEntityType, "type");
    }
}
