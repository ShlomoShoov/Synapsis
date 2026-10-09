using System.Collections.Generic;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.PackageValidation.Services.PackageRules;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.PackageValidation.PackageRules;

/// <summary>The package format (schemas/sil/schema-package.schema.json), checked on the text as sent.</summary>
public class FormatRuleTests
{
    /// <summary>The rule under test.</summary>
    private static readonly FormatRule Rule = new FormatRule();

    /// <summary>Every real package matches the format (core's country list holds NO, which must stay text).</summary>
    [Fact]
    public void Every_real_package_matches_the_format()
    {
        foreach (string name in TestPackages.NamesInRegistrationOrder())
        {
            Result result = Rule.Check(Request(TestPackages.Text(name)));
            Assert.True(result.Succeeded, $"{name}: {result.Problem?.Path} {result.Problem?.Message}");
        }
    }

    /// <summary>A section the format does not have is refused.</summary>
    [Fact]
    public void An_unknown_section_is_refused()
    {
        string text = TestPackages.Text("usgs") + "\nextras: {colour: blue}\n";
        Assert.Equal(ErrorCodes.SchemaPackage, Rule.Check(Request(text)).Problem!.Code);
    }

    /// <summary>A value of the wrong type is refused at its place.</summary>
    [Fact]
    public void A_value_of_the_wrong_type_is_refused_at_its_place()
    {
        string text = TestPackages.Text("kev").Replace("reliability: 95", "reliability: high");
        Problem problem = Rule.Check(Request(text)).Problem!;
        Assert.Equal(ErrorCodes.SchemaPackage, problem.Code);
        Assert.Equal("sources[0].reliability", problem.Path);
    }

    /// <summary>Text that is not YAML is refused.</summary>
    [Fact]
    public void Text_that_is_not_YAML_is_refused()
    {
        Assert.Equal(ErrorCodes.SchemaPackage, Rule.Check(Request("package: [unclosed")).Problem!.Code);
    }

    /// <summary>A request holding only the text that matters here (the rule reads nothing else).</summary>
    private static PackageValidationRequest Request(string text)
    {
        return new PackageValidationRequest(text, "usgs", TestPackages.Read(TestPackages.Text("usgs")), null, new List<SchemaPackage>());
    }
}
