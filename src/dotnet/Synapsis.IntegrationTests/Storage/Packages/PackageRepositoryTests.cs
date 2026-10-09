using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.IntegrationTests.Support;
using Synapsis.Sil;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Services.Descriptions;
using Synapsis.Storage.Database.Services.Dal;
using Synapsis.Storage.Packages.Abstractions;
using Synapsis.Storage.Packages.Models.Domain;
using Synapsis.Storage.Packages.Services.Repositories;
using Xunit;

namespace Synapsis.IntegrationTests.Storage.Packages;

/// <summary>The package repository against a real MongoDB: current versions only, and no two writers saving the same version.</summary>
public sealed class PackageRepositoryTests : IDisposable
{
    private readonly TestDatabase _database = new TestDatabase();
    private readonly IPackageRepository _repository;

    /// <summary>A repository over a fresh database.</summary>
    public PackageRepositoryTests()
    {
        _repository = new PackageRepository(new MongoContext(_database.Options), SilComponents.CreatePackageReader());
    }

    /// <summary>Deletes the database.</summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    /// <summary>A saved package comes back with its text, read.</summary>
    [Fact]
    public async Task A_saved_package_comes_back_read()
    {
        Assert.True((await _repository.SaveAsync(Core(1))).Succeeded);
        StoredPackage stored = Assert.Single(await _repository.AllAsync());
        Assert.Equal("core", stored.Name);
        Assert.Equal(1, stored.Version);
        Assert.Equal("core", stored.Package.Name);
        Assert.NotEmpty(stored.Package.Vocabularies);
    }

    /// <summary>Version 2 replaces version 1; nothing else is kept.</summary>
    [Fact]
    public async Task The_next_version_replaces_the_current_one()
    {
        await _repository.SaveAsync(Core(1));
        Assert.True((await _repository.SaveAsync(Core(2))).Succeeded);
        IReadOnlyList<StoredPackage> all = await _repository.AllAsync();
        Assert.Equal(2, Assert.Single(all).Version);
    }

    /// <summary>A version that does not follow the stored one means another writer was first: E-SCHEMA-VERSION.</summary>
    [Fact]
    public async Task A_save_over_a_newer_version_is_refused()
    {
        await _repository.SaveAsync(Core(1));
        await _repository.SaveAsync(Core(2));
        Assert.Equal(ErrorCodes.SchemaVersion, (await _repository.SaveAsync(Core(2))).Problem!.Code);
        Assert.Equal(ErrorCodes.SchemaVersion, (await _repository.SaveAsync(Core(1))).Problem!.Code);
    }

    /// <summary>The built-in core package as this version.</summary>
    private static StoredPackage Core(int version)
    {
        string text = DescriptionFiles.ReadText(DescriptionFiles.CorePackage);
        return new StoredPackage
        {
            Name = "core",
            Version = version,
            Fingerprint = "fp-" + version,
            Text = text,
            Package = SilComponents.CreatePackageReader().Read(text).Value!,
        };
    }
}
