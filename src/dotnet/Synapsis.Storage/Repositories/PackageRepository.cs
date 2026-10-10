using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Services.Reading;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using Synapsis.Storage.Entities;
using Synapsis.Storage.Repositories.Mappers;
namespace Synapsis.Storage.Repositories;

/// <summary>
/// The current packages in MongoDB's "schema_packages" collection, one document per package. A save names the
/// version it replaces, so two writers cannot both save version n+1.
/// </summary>
public sealed class PackageRepository : IPackageRepository
{
    /// <summary>The message when another writer saved first.</summary>
    private const string SavedFirst = "another writer saved a new version of this package first; send it again";

    private readonly IMongoCollection<PackageDocument> _packages;
    private readonly IPackageReader _reader;

    /// <summary>Creates the repository over the database connection; stored texts are read with the package reader.</summary>
    public PackageRepository(IMongoContext context, IPackageReader reader)
    {
        _packages = context.Collection<PackageDocument>(CollectionNames.SchemaPackages);
        _reader = reader;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredPackage>> AllAsync()
    {
        List<PackageDocument> documents = await _packages.Find(Builders<PackageDocument>.Filter.Empty).ToListAsync();
        List<StoredPackage> packages = new List<StoredPackage>();
        foreach (PackageDocument document in documents)
        {
            packages.Add(PackageDocumentMapper.ToStoredPackage(document, Read(document)));
        }
        return packages;
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAsync(StoredPackage package)
    {
        PackageDocument document = PackageDocumentMapper.ToDocument(package);
        if (package.Version == 1)
        {
            return await InsertFirstAsync(document);
        }
        FilterDefinition<PackageDocument> stillPrevious = Builders<PackageDocument>.Filter.Eq(stored => stored.Name, package.Name)
            & Builders<PackageDocument>.Filter.Eq(stored => stored.Version, package.Version - 1);
        ReplaceOneResult replaced = await _packages.ReplaceOneAsync(stillPrevious, document);
        if (replaced.MatchedCount == 0)
        {
            return Result.Failure(ErrorCodes.SchemaVersion, string.Empty, SavedFirst);
        }
        return Result.Success();
    }

    /// <summary>Inserts version 1; another writer's version 1 already there is E-SCHEMA-VERSION.</summary>
    private async Task<Result> InsertFirstAsync(PackageDocument document)
    {
        try
        {
            await _packages.InsertOneAsync(document);
            return Result.Success();
        }
        catch (MongoWriteException problem) when (problem.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return Result.Failure(ErrorCodes.SchemaVersion, string.Empty, SavedFirst);
        }
    }

    /// <summary>Reads a stored text. It was valid when saved, so a failure means a broken store.</summary>
    private SchemaPackage Read(PackageDocument document)
    {
        Result<SchemaPackage> read = _reader.Read(document.Text);
        if (!read.Succeeded)
        {
            throw new InvalidOperationException($"The stored package '{document.Name}' cannot be read: {read.Problem!.Message}");
        }
        return read.Value!;
    }
}
