using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.PackageValidation.Services.PackageRules;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.PackageValidation.PackageRules;

/// <summary>A new version may add, never remove, and may not change a measure's unit (decision S7).</summary>
public class CompatibilityRuleTests
{
    /// <summary>The rule under test.</summary>
    private static readonly CompatibilityRule Rule = new CompatibilityRule();

    /// <summary>A package with no stored version has nothing to stay compatible with.</summary>
    [Fact]
    public void A_new_package_passes()
    {
        Assert.True(Rule.Check(TestPackages.NewRequest("usgs", TestPackages.Text("usgs"))).Succeeded);
    }

    /// <summary>Adding a measure, or lowering a maximum, is allowed.</summary>
    [Theory]
    [InlineData("measures: [magnitude, depth_km]", "measures: [magnitude, depth_km, felt_reports]")]
    [InlineData("min: -10, max: 800}", "min: -10, max: 700}")]
    public void Additions_and_other_narrowings_pass(string before, string after)
    {
        string text = TestPackages.Text("usgs").Replace(before, after);
        if (after.Contains("felt_reports"))
        {
            text += "\n  - {name: felt_reports, title: Felt reports, unit: count}\n";
        }
        Result result = Rule.Check(TestPackages.UpdateRequest("usgs", text));
        Assert.True(result.Succeeded, result.Problem?.Message);
    }

    /// <summary>Removing a measure from an event type is breaking.</summary>
    [Fact]
    public void A_measure_removed_from_an_event_type_is_refused()
    {
        string text = TestPackages.Text("usgs").Replace("measures: [magnitude, depth_km]", "measures: [magnitude]");
        Problem problem = Rule.Check(TestPackages.UpdateRequest("usgs", text)).Problem!;
        Assert.Equal(ErrorCodes.SchemaBreaking, problem.Code);
        Assert.Contains("depth_km", problem.Message);
    }

    /// <summary>A measure whose unit changes is breaking: stored values would mean something else.</summary>
    [Fact]
    public void A_unit_change_is_refused()
    {
        string text = TestPackages.Text("usgs").Replace("unit: km,", "unit: m,");
        Assert.Equal(ErrorCodes.SchemaBreaking, Rule.Check(TestPackages.UpdateRequest("usgs", text)).Problem!.Code);
    }

    /// <summary>A removed event type is breaking.</summary>
    [Fact]
    public void A_removed_event_type_is_refused()
    {
        PackageValidationRequest request = TestPackages.UpdateRequest("usgs", TestPackages.Text("usgs"));
        request.Package.EventTypes.Clear();
        Assert.Equal(ErrorCodes.SchemaBreaking, Rule.Check(request).Problem!.Code);
    }

    /// <summary>A value removed from a closed logical space is breaking.</summary>
    [Fact]
    public void A_value_removed_from_a_logical_space_is_refused()
    {
        PackageValidationRequest request = TestPackages.UpdateRequest("kev-classes", TestPackages.Text("kev-classes"));
        request.Package.LogicalSpaces[0].Values.RemoveAt(0);
        Assert.Equal(ErrorCodes.SchemaBreaking, Rule.Check(request).Problem!.Code);
    }
}
