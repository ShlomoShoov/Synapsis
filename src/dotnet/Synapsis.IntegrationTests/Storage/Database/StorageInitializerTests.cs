using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.IntegrationTests.Support;
using Synapsis.Storage.Database.Models.Descriptions;
using Synapsis.Storage.Database.Services.Dal;
using Synapsis.Storage.Database.Services.Initializers;
using Xunit;

namespace Synapsis.IntegrationTests.Storage.Database;

/// <summary>The start-up preparation of the database.</summary>
public sealed class StorageInitializerTests : IDisposable
{
    private readonly TestDatabase _database = new TestDatabase();

    /// <summary>Deletes the database.</summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    /// <summary>The records index exists after start-up, and starting again changes nothing.</summary>
    [Fact]
    public async Task The_index_is_created_and_starting_again_is_harmless()
    {
        MongoContext context = new MongoContext(_database.Options);
        StorageInitializer initializer = new StorageInitializer(context);
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();
        List<BsonDocument> indexes = await (await context.Collection<BsonDocument>(CollectionNames.Records).Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["key"].AsBsonDocument.Contains("source"));
        Assert.Equal(2, indexes.Count);
    }
}
