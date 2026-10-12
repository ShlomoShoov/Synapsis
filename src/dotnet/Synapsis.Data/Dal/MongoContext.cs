using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>
/// The connection to MongoDB and its two collections, made once per process (the driver's client is thread-safe and
/// keeps a pool of connections).
/// </summary>
public sealed class MongoContext
{
    private readonly IMongoDatabase _database;

    /// <summary>Connects to the database the options name.</summary>
    public MongoContext(MongoOptions options)
    {
        MongoClient client = new MongoClient(options.Url);
        _database = client.GetDatabase(options.Database);
        Records = _database.GetCollection<StoredRecord>("records");
        Catalog = _database.GetCollection<CatalogEntry>("catalog");
    }

    /// <summary>Every state of every record.</summary>
    public IMongoCollection<StoredRecord> Records { get; }

    /// <summary>Every value that has entered the system (sources and types).</summary>
    public IMongoCollection<CatalogEntry> Catalog { get; }

    /// <summary>Asks MongoDB to answer; throws when it does not.</summary>
    public Task PingAsync()
    {
        return _database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }");
    }
}
