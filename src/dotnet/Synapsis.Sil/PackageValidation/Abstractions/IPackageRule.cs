using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Models.Requests;

namespace Synapsis.Sil.PackageValidation.Abstractions;

/// <summary>One check of a schema package. The checks run in a fixed order; the first failure is the answer.</summary>
public interface IPackageRule
{
    /// <summary>Passes, or fails with the problem found.</summary>
    Result Check(PackageValidationRequest request);
}
