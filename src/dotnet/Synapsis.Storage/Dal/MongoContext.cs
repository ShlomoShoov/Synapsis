using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Synapsis.Storage.Models;
namespace Synapsis.Storage.Dal;

/// <summary>
/// The connection to MongoDB, made once per process (the driver's client is thread-safe and pools its connections).
/// </summary>
public sealed class MongoContext : IMongoContext
{
    private readonly IMongoDatabase _database;

    /// <summary>Connects to the database the options name.</summary>
    public MongoContext(MongoOptions options)
    {
        MongoClient client = new MongoClient(options.Url);
        _database = client.GetDatabase(options.Database);
    }

    /// <inheritdoc/>
    public IMongoCollection<TDocument> Collection<TDocument>(string name)
    {
        return _database.GetCollection<TDocument>(name);
    }

    /// <inheritdoc/>
    public Task PingAsync()
    {
        return _database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }");
    }
}
