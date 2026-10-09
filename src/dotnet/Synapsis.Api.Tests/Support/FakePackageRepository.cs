using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Storage.Packages.Abstractions;
using Synapsis.Storage.Packages.Models.Domain;

namespace Synapsis.Api.Tests.Support;

/// <summary>A package store in memory, with the same version check as the real one.</summary>
public sealed class FakePackageRepository : IPackageRepository
{
    /// <summary>The stored packages by name.</summary>
    public Dictionary<string, StoredPackage> Stored { get; } = new Dictionary<string, StoredPackage>();

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredPackage>> AllAsync()
    {
        IReadOnlyList<StoredPackage> all = new List<StoredPackage>(Stored.Values);
        return Task.FromResult(all);
    }

    /// <inheritdoc/>
    public Task<Result> SaveAsync(StoredPackage package)
    {
        int previous = 0;
        if (Stored.TryGetValue(package.Name, out StoredPackage? current))
        {
            previous = current.Version;
        }
        if (package.Version != previous + 1)
        {
            return Task.FromResult(Result.Failure(ErrorCodes.SchemaVersion, string.Empty, "another writer saved first"));
        }
        Stored[package.Name] = package;
        return Task.FromResult(Result.Success());
    }
}
