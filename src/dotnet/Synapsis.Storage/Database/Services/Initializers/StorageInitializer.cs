using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Storage.Database.Abstractions;
using Synapsis.Storage.Database.Models.Descriptions;
using Synapsis.Storage.Records.Models.Entities;

namespace Synapsis.Storage.Database.Services.Initializers;

/// <summary>
/// Creates the one index the record queries need: (id, source, version). It serves "does this id exist?", "the stored
/// versions of this statement" and "this id as of a date". Schema packages are found by their _id, which MongoDB
/// always indexes. Creating an index that exists does nothing, so this runs at every start.
/// </summary>
public sealed class StorageInitializer : IStorageInitializer
{
    private readonly IMongoContext _context;

    /// <summary>Creates the initializer over the database connection.</summary>
    public StorageInitializer(IMongoContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        IMongoCollection<RecordDocument> records = _context.Collection<RecordDocument>(CollectionNames.Records);
        IndexKeysDefinition<RecordDocument> keys = Builders<RecordDocument>.IndexKeys
            .Ascending(document => document.Id)
            .Ascending(document => document.Source)
            .Descending(document => document.Version);
        await records.Indexes.CreateOneAsync(new CreateIndexModel<RecordDocument>(keys));
    }
}
