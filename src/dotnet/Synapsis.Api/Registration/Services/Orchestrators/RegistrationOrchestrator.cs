using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Synapsis.Api.Registration.Abstractions;
using Synapsis.Api.Registration.Models.Requests;
using Synapsis.Api.Registration.Models.Results;
using Synapsis.Api.Vocabulary.Abstractions;
using Synapsis.Sil.Fingerprinting.Abstractions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Abstractions;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Storage.Packages.Abstractions;
using Synapsis.Storage.Packages.Models.Domain;

namespace Synapsis.Api.Registration.Services.Orchestrators;

/// <summary>
/// The registration process: read → compare with the stored version (same fingerprint: unchanged) → validate against
/// the other packages → save as the next version → put the new vocabulary in force. One registration at a time, so two
/// packages can never claim the same code at the same moment.
/// </summary>
public sealed class RegistrationOrchestrator : IRegistrationOrchestrator
{
    private readonly SemaphoreSlim _oneAtATime = new SemaphoreSlim(1, 1);
    private readonly IPackageReader _reader;
    private readonly IPackageRepository _packages;
    private readonly IFingerprinter _fingerprinter;
    private readonly IPackageValidator _validator;
    private readonly IVocabularyProvider _vocabulary;

    /// <summary>Creates the orchestrator from the services it coordinates.</summary>
    public RegistrationOrchestrator(IPackageReader reader, IPackageRepository packages, IFingerprinter fingerprinter, IPackageValidator validator, IVocabularyProvider vocabulary)
    {
        _reader = reader;
        _packages = packages;
        _fingerprinter = fingerprinter;
        _validator = validator;
        _vocabulary = vocabulary;
    }

    /// <inheritdoc/>
    public async Task<Result<RegistrationResult>> RegisterAsync(RegistrationRequest request)
    {
        await _oneAtATime.WaitAsync();
        try
        {
            return await RegisterOneAsync(request);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    /// <summary>The steps of one registration.</summary>
    private async Task<Result<RegistrationResult>> RegisterOneAsync(RegistrationRequest request)
    {
        Result<SchemaPackage> read = _reader.Read(request.Text);
        if (!read.Succeeded)
        {
            return Result<RegistrationResult>.Failure(read.Problem!);
        }
        IReadOnlyList<StoredPackage> stored = await _packages.AllAsync();
        StoredPackage? current = Find(stored, request.Name);
        string fingerprint = _fingerprinter.OfPackage(read.Value!);
        if (current != null && current.Fingerprint == fingerprint)
        {
            return Result<RegistrationResult>.Success(new RegistrationResult(RegistrationStatus.Unchanged, request.Name, current.Version));
        }
        PackageValidationRequest validation = new PackageValidationRequest(request.Text, request.Name, read.Value!, current?.Package, Others(stored, request.Name));
        Result<IRegistry> vocabulary = _validator.Validate(validation);
        if (!vocabulary.Succeeded)
        {
            return Result<RegistrationResult>.Failure(vocabulary.Problem!);
        }
        StoredPackage next = new StoredPackage
        {
            Name = request.Name,
            Version = current == null ? 1 : current.Version + 1,
            Fingerprint = fingerprint,
            Text = request.Text,
            Package = read.Value!,
        };
        Result saved = await _packages.SaveAsync(next);
        if (!saved.Succeeded)
        {
            return Result<RegistrationResult>.Failure(saved.Problem!);
        }
        _vocabulary.Replace(vocabulary.Value!);
        RegistrationStatus status = current == null ? RegistrationStatus.Created : RegistrationStatus.Updated;
        return Result<RegistrationResult>.Success(new RegistrationResult(status, request.Name, next.Version));
    }

    /// <summary>The stored package with this name, or null.</summary>
    private static StoredPackage? Find(IReadOnlyList<StoredPackage> stored, string name)
    {
        foreach (StoredPackage package in stored)
        {
            if (package.Name == name)
            {
                return package;
            }
        }
        return null;
    }

    /// <summary>Every stored package except the one with this name.</summary>
    private static List<SchemaPackage> Others(IReadOnlyList<StoredPackage> stored, string name)
    {
        List<SchemaPackage> others = new List<SchemaPackage>();
        foreach (StoredPackage package in stored)
        {
            if (package.Name != name)
            {
                others.Add(package.Package);
            }
        }
        return others;
    }
}
