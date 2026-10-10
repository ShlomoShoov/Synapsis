using System.Threading.Tasks;
using MongoDB.Driver;

namespace Synapsis.Storage.Dal;

/// <summary>
/// The data access layer: the one connection to the MongoDB database. It knows collections, not records or packages;
/// the repositories use it to answer domain questions.
/// </summary>
public interface IMongoContext
{
    /// <summary>A collection of the database, its documents read and written as <typeparamref name="TDocument"/>.</summary>
    IMongoCollection<TDocument> Collection<TDocument>(string name);

    /// <summary>Asks MongoDB to answer; throws when it does not.</summary>
    Task PingAsync();
}
