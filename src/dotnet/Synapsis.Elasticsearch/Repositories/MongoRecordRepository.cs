using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;

namespace Synapsis.Elasticsearch;

/// <summary>Reads one stored record from MongoDB by the document key received from Kafka.</summary>
public sealed class MongoRecordRepository
{
    private readonly IMongoCollection<StoredRecord> _records;

    /// <summary>Creates the repository over the shared MongoDB records collection.</summary>
    public MongoRecordRepository(MongoContext mongoContext)
    {
        _records = mongoContext.Records;
    }

    /// <summary>Finds one stored record, or null when the key is not present.</summary>
    public async Task<StoredRecord?> FindAsync(string documentKey, CancellationToken cancellationToken)
    {
        FilterDefinition<StoredRecord> filter = Builders<StoredRecord>.Filter.Eq(record => record.Key, documentKey);
        return await _records.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }
}
