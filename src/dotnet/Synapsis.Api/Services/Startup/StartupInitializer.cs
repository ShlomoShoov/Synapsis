using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synapsis.Api.Orchestrators;
using Synapsis.Api.Models.Registration;
using Synapsis.Api.Services.Vocabulary;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Registry;
using Synapsis.Sil.Services.Descriptions;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Repositories;
using Synapsis.Storage.Models;
namespace Synapsis.Api.Services.Startup;

/// <summary>
/// Runs once when the server starts, before it accepts requests: prepares the database, registers the built-in core
/// package when the store lacks it (through the same registration as an API call), and loads the vocabulary from every
/// stored package. If any step fails the server does not start, and Docker starts it again.
/// </summary>
public sealed class StartupInitializer : IHostedService
{
    /// <summary>The name of the system's own package.</summary>
    private const string CoreName = "core";

    private readonly IStorageInitializer _storage;
    private readonly IPackageRepository _packages;
    private readonly IRegistrationOrchestrator _registration;
    private readonly IRegistryBuilder _registryBuilder;
    private readonly IVocabularyProvider _vocabulary;
    private readonly ILogger<StartupInitializer> _log;

    /// <summary>Creates the initializer from the services it uses.</summary>
    public StartupInitializer(IStorageInitializer storage, IPackageRepository packages, IRegistrationOrchestrator registration, IRegistryBuilder registryBuilder, IVocabularyProvider vocabulary, ILogger<StartupInitializer> log)
    {
        _storage = storage;
        _packages = packages;
        _registration = registration;
        _registryBuilder = registryBuilder;
        _vocabulary = vocabulary;
        _log = log;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _storage.InitializeAsync();
        IReadOnlyList<StoredPackage> stored = await _packages.AllAsync();
        if (!HasCore(stored))
        {
            await SeedCoreAsync();
            stored = await _packages.AllAsync();
        }
        LoadVocabulary(stored);
        _log.LogInformation("Vocabulary loaded from {Count} packages", stored.Count);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Registers the built-in core package.</summary>
    private async Task SeedCoreAsync()
    {
        string text = DescriptionFiles.ReadText(DescriptionFiles.CorePackage);
        Result<RegistrationResult> seeded = await _registration.RegisterAsync(new RegistrationRequest(CoreName, text));
        if (!seeded.Succeeded)
        {
            throw new InvalidOperationException($"The core package could not be registered: {seeded.Problem!.Code} {seeded.Problem.Message}");
        }
        _log.LogInformation("Core package registered, version {Version}", seeded.Value!.Version);
    }

    /// <summary>Builds the vocabulary from the stored packages and puts it in force.</summary>
    private void LoadVocabulary(IReadOnlyList<StoredPackage> stored)
    {
        List<SchemaPackage> packages = new List<SchemaPackage>();
        foreach (StoredPackage package in stored)
        {
            packages.Add(package.Package);
        }
        Result<IRegistry> vocabulary = _registryBuilder.Build(packages);
        if (!vocabulary.Succeeded)
        {
            throw new InvalidOperationException($"The stored packages do not form a vocabulary: {vocabulary.Problem!.Message}");
        }
        _vocabulary.Replace(vocabulary.Value!);
    }

    /// <summary>True when the core package is stored.</summary>
    private static bool HasCore(IReadOnlyList<StoredPackage> stored)
    {
        foreach (StoredPackage package in stored)
        {
            if (package.Name == CoreName)
            {
                return true;
            }
        }
        return false;
    }
}
