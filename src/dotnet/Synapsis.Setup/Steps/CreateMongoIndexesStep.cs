using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;

namespace Synapsis.Setup;

/// <summary>Creates the index the record queries need in MongoDB: (id, source, state).</summary>
public sealed class CreateMongoIndexesStep : ISetupStep
{
    private readonly MongoContext _mongoContext;

    /// <summary>Creates the step over the MongoDB connection.</summary>
    public CreateMongoIndexesStep(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    /// <summary>Creates the index; MongoDB does nothing when the same index already exists.</summary>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        IndexKeysDefinition<StoredRecord> keys = Builders<StoredRecord>.IndexKeys
            .Ascending(record => record.RecordId)
            .Ascending(record => record.Source)
            .Descending(record => record.State);
        return _mongoContext.Records.Indexes.CreateOneAsync(new CreateIndexModel<StoredRecord>(keys), cancellationToken: cancellationToken);
    }
}
