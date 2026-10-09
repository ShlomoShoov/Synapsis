using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.PackageValidation.Services.PackageRules;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.PackageValidation.PackageRules;

/// <summary>A package must carry the name it is registered under.</summary>
public class NameRuleTests
{
    /// <summary>The rule under test.</summary>
    private static readonly NameRule Rule = new NameRule();

    /// <summary>The matching name passes.</summary>
    [Fact]
    public void The_matching_name_passes()
    {
        Assert.True(Rule.Check(TestPackages.NewRequest("usgs", TestPackages.Text("usgs"))).Succeeded);
    }

    /// <summary>Another package's name is refused at "package".</summary>
    [Fact]
    public void Another_name_is_refused()
    {
        string text = TestPackages.Text("usgs");
        PackageValidationRequest request = new PackageValidationRequest(text, "kev", TestPackages.Read(text), null, TestPackages.AllExcept("usgs"));
        Problem problem = Rule.Check(request).Problem!;
        Assert.Equal(ErrorCodes.SchemaPackage, problem.Code);
        Assert.Equal("package", problem.Path);
    }
}
