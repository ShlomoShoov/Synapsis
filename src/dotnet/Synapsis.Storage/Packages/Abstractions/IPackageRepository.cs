using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Results;
using Synapsis.Storage.Packages.Models.Domain;

namespace Synapsis.Storage.Packages.Abstractions;

/// <summary>Keeps the current version of each schema package (no history: decision P4).</summary>
public interface IPackageRepository
{
    /// <summary>Every current package, read; empty when none is stored.</summary>
    Task<IReadOnlyList<StoredPackage>> AllAsync();

    /// <summary>
    /// Saves a package as its new current version. It succeeds only if the stored version is still the one before it
    /// (none, for version 1); otherwise another writer saved first and the answer is E-SCHEMA-VERSION.
    /// </summary>
    Task<Result> SaveAsync(StoredPackage package);
}
