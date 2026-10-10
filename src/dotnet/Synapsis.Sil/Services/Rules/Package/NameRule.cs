using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Package;

/// <summary>The package must carry the name it is registered under, so one package cannot overwrite another.</summary>
public sealed class NameRule : IPackageRule
{
    /// <summary>Passes when the names match; E-SCHEMA-PACKAGE at "package" otherwise.</summary>
    public Result Check(PackageValidationRequest request)
    {
        if (request.Package.Name == request.ExpectedName)
        {
            return Result.Success();
        }
        string message = $"the package is named '{request.Package.Name}' but was sent as '{request.ExpectedName}'";
        return Result.Failure(ErrorCodes.SchemaPackage, "package", message);
    }
}
