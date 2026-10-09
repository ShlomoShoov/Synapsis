using System.Collections.Generic;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Abstractions;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.PackageValidation.Services.Orchestrators;

/// <summary>
/// Runs the package rules in order (see SilComponents), then builds the vocabulary from the other packages and this
/// one. Building refuses a code that two packages define, so it is also the ownership check (E-SCHEMA-OWNERSHIP).
/// </summary>
public sealed class PackageValidator : IPackageValidator
{
    private readonly IReadOnlyList<IPackageRule> _rules;
    private readonly IRegistryBuilder _registryBuilder;

    /// <summary>Creates the validator from its parts, the rules in the order they must run.</summary>
    public PackageValidator(IReadOnlyList<IPackageRule> rules, IRegistryBuilder registryBuilder)
    {
        _rules = rules;
        _registryBuilder = registryBuilder;
    }

    /// <inheritdoc/>
    public Result<IRegistry> Validate(PackageValidationRequest request)
    {
        foreach (IPackageRule rule in _rules)
        {
            Result result = rule.Check(request);
            if (!result.Succeeded)
            {
                return Result<IRegistry>.Failure(result.Problem!);
            }
        }
        List<SchemaPackage> all = new List<SchemaPackage>(request.Others);
        all.Add(request.Package);
        return _registryBuilder.Build(all);
    }
}
