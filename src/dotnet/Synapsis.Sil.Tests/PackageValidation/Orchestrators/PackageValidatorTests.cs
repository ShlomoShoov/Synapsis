using System.Collections.Generic;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Abstractions;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.PackageValidation.Orchestrators;

/// <summary>The package validator as a whole: the real packages pass, and ownership is checked when the vocabulary is built.</summary>
public class PackageValidatorTests
{
    /// <summary>The validator, through its abstraction.</summary>
    private static readonly IPackageValidator Validator = SilComponents.CreatePackageValidator();

    /// <summary>Every real package registers in order, each against the ones registered before it.</summary>
    [Fact]
    public void Every_real_package_registers_in_order()
    {
        List<SchemaPackage> registered = new List<SchemaPackage>();
        foreach (string name in TestPackages.NamesInRegistrationOrder())
        {
            string text = TestPackages.Text(name);
            SchemaPackage package = TestPackages.Read(text);
            Result<IRegistry> result = Validator.Validate(new PackageValidationRequest(text, name, package, null, registered));
            Assert.True(result.Succeeded, $"{name}: {result.Problem?.Code} {result.Problem?.Message}");
            registered.Add(package);
        }
    }

    /// <summary>The same package sent again over its stored version passes, and gives the vocabulary with it.</summary>
    [Fact]
    public void A_package_sent_again_passes()
    {
        Result<IRegistry> result = Validator.Validate(TestPackages.UpdateRequest("usgs", TestPackages.Text("usgs")));
        Assert.True(result.Succeeded, result.Problem?.Message);
        Assert.NotNull(result.Value!.FindEventType("GEO.EARTHQUAKE"));
    }

    /// <summary>A package declaring codes another package owns is E-SCHEMA-OWNERSHIP.</summary>
    [Fact]
    public void Codes_owned_by_another_package_are_refused()
    {
        string copy = TestPackages.Text("usgs").Replace("package: usgs", "package: usgs-copy");
        PackageValidationRequest request = new PackageValidationRequest(copy, "usgs-copy", TestPackages.Read(copy), null, TestPackages.AllExcept("none"));
        Assert.Equal(ErrorCodes.SchemaOwnership, Validator.Validate(request).Problem!.Code);
    }
}
