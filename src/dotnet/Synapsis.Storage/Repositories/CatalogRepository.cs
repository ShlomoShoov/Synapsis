using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;

namespace Synapsis.Storage.Repositories;

/// <summary>
/// The catalog in MongoDB's "catalog" collection: every source and every type that has entered the system, with how
/// many record states were stored with it.
/// </summary>
public sealed class CatalogRepository
{
    private readonly IMongoCollection<CatalogEntry> _catalogCollection;

    /// <summary>Creates the repository over the database connection.</summary>
    public CatalogRepository(MongoContext mongoContext)
    {
        _catalogCollection = mongoContext.Catalog;
    }

    /// <summary>Adds the sources and types of the stored records: a new value is created, a known one counted up.</summary>
    public async Task AddAsync(IReadOnlyList<StoredRecord> storedRecords)
    {
        if (storedRecords.Count == 0)
        {
            return;
        }

        // 1. Count the records per value: each record adds its source and its type ("event_type", ...).
        Dictionary<string, CatalogEntry> entries = new Dictionary<string, CatalogEntry>();
        foreach (StoredRecord stored in storedRecords)
        {
            AddOne(entries, "source", stored.Source);
            AddOne(entries, stored.Kind + "_type", stored.Type);
        }

        // 2. One write: create each value the first time, count it up and set when it was last seen.
        DateTime now = DateTime.UtcNow;
        List<WriteModel<CatalogEntry>> writes = new List<WriteModel<CatalogEntry>>();
        foreach (CatalogEntry entry in entries.Values)
        {
            FilterDefinition<CatalogEntry> filter = Builders<CatalogEntry>.Filter.Eq(stored => stored.Key, entry.Key);
            UpdateDefinition<CatalogEntry> update = Builders<CatalogEntry>.Update
                .SetOnInsert(stored => stored.Field, entry.Field)
                .SetOnInsert(stored => stored.Value, entry.Value)
                .Inc(stored => stored.Count, entry.Count)
                .Set(stored => stored.LastSeen, now);
            writes.Add(new UpdateOneModel<CatalogEntry>(filter, update) { IsUpsert = true });
        }
        await _catalogCollection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
    }

    /// <summary>Every catalog entry, sorted by field, then value.</summary>
    public Task<List<CatalogEntry>> AllAsync()
    {
        SortDefinition<CatalogEntry> order = Builders<CatalogEntry>.Sort.Ascending(entry => entry.Field).Ascending(entry => entry.Value);
        return _catalogCollection.Find(Builders<CatalogEntry>.Filter.Empty).Sort(order).ToListAsync();
    }

    /// <summary>Counts one more record for the value, adding the value to the list the first time.</summary>
    private static void AddOne(Dictionary<string, CatalogEntry> entries, string field, string value)
    {
        string key = field + ":" + value;
        if (!entries.TryGetValue(key, out CatalogEntry? entry))
        {
            entry = new CatalogEntry { Key = key, Field = field, Value = value };
            entries.Add(key, entry);
        }
        entry.Count++;
    }
}
