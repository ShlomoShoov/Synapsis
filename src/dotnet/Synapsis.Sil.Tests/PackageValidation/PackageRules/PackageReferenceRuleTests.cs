using System.Collections.Generic;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.PackageValidation.Services.PackageRules;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.PackageValidation.PackageRules;

/// <summary>Every code a package points to must exist in some package.</summary>
public class PackageReferenceRuleTests
{
    /// <summary>The rule under test.</summary>
    private static readonly PackageReferenceRule Rule = new PackageReferenceRule();

    /// <summary>A real package's references all resolve against the others.</summary>
    [Fact]
    public void A_real_package_resolves()
    {
        Assert.True(Rule.Check(TestPackages.NewRequest("kev", TestPackages.Text("kev"))).Succeeded);
    }

    /// <summary>A role no package declares is E-SCHEMA-REF.</summary>
    [Fact]
    public void An_unknown_role_is_refused()
    {
        PackageValidationRequest request = TestPackages.NewRequest("kev", TestPackages.Text("kev"));
        request.Package.EventTypes[0].Roles["bystander"] = new List<string>();
        Assert.Equal(ErrorCodes.SchemaRef, Rule.Check(request).Problem!.Code);
    }

    /// <summary>A measure no package declares is E-SCHEMA-REF.</summary>
    [Fact]
    public void An_unknown_measure_is_refused()
    {
        PackageValidationRequest request = TestPackages.NewRequest("usgs", TestPackages.Text("usgs"));
        request.Package.EventTypes[0].Measures.Add("felt_reports");
        Assert.Equal(ErrorCodes.SchemaRef, Rule.Check(request).Problem!.Code);
    }

    /// <summary>A shared namespace (owned by core) cannot be a package's own entity namespace.</summary>
    [Fact]
    public void A_shared_namespace_cannot_be_owned()
    {
        PackageValidationRequest request = TestPackages.NewRequest("kev", TestPackages.Text("kev"));
        request.Package.Namespaces.Entities.Add("cve");
        Assert.Equal(ErrorCodes.SchemaRef, Rule.Check(request).Problem!.Code);
    }
}
