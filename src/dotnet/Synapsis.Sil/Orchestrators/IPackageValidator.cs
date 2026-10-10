using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Services.Registry;
namespace Synapsis.Sil.Orchestrators;

/// <summary>
/// Decides whether a schema package may become current: its format, its name, that it removes nothing its stored
/// version declared, that every code it points to exists, and that no code is owned by two packages. When it may, the
/// answer is the vocabulary that will be in force with it.
/// </summary>
public interface IPackageValidator
{
    /// <summary>
    /// The vocabulary of the other packages plus this one; or E-SCHEMA-PACKAGE, E-SCHEMA-BREAKING, E-SCHEMA-REF or
    /// E-SCHEMA-OWNERSHIP, the first problem found.
    /// </summary>
    Result<IRegistry> Validate(PackageValidationRequest request);
}
