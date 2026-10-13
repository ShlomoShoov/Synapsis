using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Entities;

namespace Synapsis.Api.Repositories;

/// <summary>The record states in MongoDB's "records" collection: find the latest ones, add new ones, read as of a date.</summary>
public sealed class RecordRepository
{
    private readonly IMongoCollection<StoredRecord> _recordCollection;

    /// <summary>Creates the repository over the database connection.</summary>
    public RecordRepository(MongoContext mongoContext)
    {
        _recordCollection = mongoContext.Records;
    }

    /// <summary>
    /// Every stored state of each record (same id and source) among the given ones, by key "{id}|{source}".
    /// A record never stored is missing from the answer. The record bodies are not loaded, only the fields around them.
    /// </summary>
    public async Task<Dictionary<string, List<StoredRecord>>> StatesByKeyAsync(IReadOnlyList<StoredRecord> records)
    {
        // 1. One query: every stored state of the records' ids, without the record bodies.
        HashSet<string> recordIds = new HashSet<string>();
        foreach (StoredRecord record in records)
        {
            recordIds.Add(record.RecordId);
        }
        FilterDefinition<StoredRecord> filter = Builders<StoredRecord>.Filter.In(stored => stored.RecordId, recordIds);
        ProjectionDefinition<StoredRecord> withoutBody = Builders<StoredRecord>.Projection.Exclude(stored => stored.Record);
        List<StoredRecord> storedStates = await _recordCollection.Find(filter).Project<StoredRecord>(withoutBody).ToListAsync();

        // 2. Group them by (id, source).
        Dictionary<string, List<StoredRecord>> statesByKey = new Dictionary<string, List<StoredRecord>>();
        foreach (StoredRecord stored in storedStates)
        {
            string key = StoredRecord.KeyOf(stored.RecordId, stored.Source);
            if (!statesByKey.ContainsKey(key))
            {
                statesByKey.Add(key, new List<StoredRecord>());
            }
            statesByKey[key].Add(stored);
        }
        return statesByKey;
    }

    /// <summary>Stores the given record states in one call. Nothing to store: nothing is sent.</summary>
    public async Task AddAsync(IReadOnlyList<StoredRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }
        await _recordCollection.InsertManyAsync(records, new InsertManyOptions { IsOrdered = false });
    }

    /// <summary>
    /// For each source, the latest state of the record that was known at the given moment (known_at not after it).
    /// No moment: the latest state. A source name keeps that source only.
    /// </summary>
    public async Task<List<StoredRecord>> AsOfAsync(string recordId, DateTime? asOf, string? source)
    {
        // 1. Every state of the record known at the moment, newest state first within each source.
        FilterDefinition<StoredRecord> filter = Builders<StoredRecord>.Filter.Eq(stored => stored.RecordId, recordId);
        if (asOf != null)
        {
            filter = filter & Builders<StoredRecord>.Filter.Lte(stored => stored.KnownAt, asOf.Value);
        }
        if (source != null)
        {
            filter = filter & Builders<StoredRecord>.Filter.Eq(stored => stored.Source, source);
        }
        SortDefinition<StoredRecord> order = Builders<StoredRecord>.Sort.Ascending(stored => stored.Source).Descending(stored => stored.State);
        List<StoredRecord> states = await _recordCollection.Find(filter).Sort(order).ToListAsync();

        // 2. Keep the first (the newest) of each source.
        List<StoredRecord> latestPerSource = new List<StoredRecord>();
        foreach (StoredRecord state in states)
        {
            if (latestPerSource.Count == 0 || latestPerSource[latestPerSource.Count - 1].Source != state.Source)
            {
                latestPerSource.Add(state);
            }
        }
        return latestPerSource;
    }

    /// <summary>Finds the selected state of each requested record and source at the given UTC moment.</summary>
    public async Task<Dictionary<string, List<StoredRecord>>> AsOfManyAsync(IReadOnlyCollection<string> recordIds, DateTime asOf)
    {
        Dictionary<string, List<StoredRecord>> recordsById = new Dictionary<string, List<StoredRecord>>();
        if (recordIds.Count == 0)
        {
            return recordsById;
        }

        // 1. Query bounded batches, in the order needed to select one state per source.
        List<string> distinctIds = new List<string>(new HashSet<string>(recordIds));
        const int batchSize = 500;
        for (int start = 0; start < distinctIds.Count; start += batchSize)
        {
            int count = Math.Min(batchSize, distinctIds.Count - start);
            List<string> batch = distinctIds.GetRange(start, count);
            await AddAsOfBatchAsync(batch, asOf, recordsById);
        }
        return recordsById;
    }

    /// <summary>Adds the first known state for each source in one sorted batch to the result dictionary.</summary>
    private async Task AddAsOfBatchAsync(List<string> recordIds, DateTime asOf, Dictionary<string, List<StoredRecord>> recordsById)
    {
        FilterDefinition<StoredRecord> filter = Builders<StoredRecord>.Filter.In(stored => stored.RecordId, recordIds)
            & Builders<StoredRecord>.Filter.Lte(stored => stored.KnownAt, asOf);
        SortDefinition<StoredRecord> order = Builders<StoredRecord>.Sort.Ascending(stored => stored.RecordId)
            .Ascending(stored => stored.Source).Descending(stored => stored.State);
        List<StoredRecord> states = await _recordCollection.Find(filter).Sort(order).ToListAsync();
        string? previousId = null;
        string? previousSource = null;
        foreach (StoredRecord state in states)
        {
            if (state.RecordId == previousId && state.Source == previousSource)
            {
                continue;
            }
            if (!recordsById.ContainsKey(state.RecordId))
            {
                recordsById.Add(state.RecordId, new List<StoredRecord>());
            }
            recordsById[state.RecordId].Add(state);
            previousId = state.RecordId;
            previousSource = state.Source;
        }
    }
}
