using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 4: id prefix and namespace ownership.</summary>
public class IdentityRuleTests
{
    /// <summary>An event id with an entity prefix is E-ID-NAMESPACE.</summary>
    [Fact]
    public void A_prefix_of_another_kind_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["id"] = "ent:usgs:quake";
        RuleCheck.Refused(record, ErrorCodes.IdNamespace, "id");
    }

    /// <summary>A namespace owned by another package is E-ID-NAMESPACE.</summary>
    [Fact]
    public void A_namespace_of_another_package_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["id"] = "evt:kev:quake";
        RuleCheck.Refused(record, ErrorCodes.IdNamespace, "id");
    }

    /// <summary>A key that does not fit the shared namespace's format is E-ID-NAMESPACE.</summary>
    [Fact]
    public void A_malformed_shared_key_is_refused()
    {
        JsonObject record = TestRecords.CveEntity();
        record["id"] = "ent:cve:CVE-99";
        RuleCheck.Refused(record, ErrorCodes.IdNamespace, "id");
    }

    /// <summary>A shared namespace only holds its own entity type.</summary>
    [Fact]
    public void A_wrong_type_in_a_shared_namespace_is_refused()
    {
        JsonObject record = TestRecords.CveEntity();
        record["type"] = "Vendor";
        RuleCheck.Refused(record, ErrorCodes.IdNamespace, "id");
    }
}
