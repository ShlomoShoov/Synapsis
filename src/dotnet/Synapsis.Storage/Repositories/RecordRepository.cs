using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Versioning;
using Synapsis.Storage.Dal;
using Synapsis.Storage.Models;
using Synapsis.Storage.Entities;
using Synapsis.Storage.Repositories.Mappers;
namespace Synapsis.Storage.Repositories;

/// <summary>
/// The record versions in MongoDB's "records" collection. Lists of ids are asked in chunks, so one batch's questions
/// stay a few round trips however many ids it names.
/// </summary>
public sealed class RecordRepository : IRecordRepository
{
    /// <summary>The most ids one query names.</summary>
    private const int ChunkSize = 20000;

    /// <summary>The kind of the documents that hold entities.</summary>
    private const string EntityKind = "entity";

    private readonly IMongoCollection<RecordDocument> _records;

    /// <summary>Creates the repository over the database connection.</summary>
    public RecordRepository(IMongoContext context)
    {
        _records = context.Collection<RecordDocument>(CollectionNames.Records);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>>> StoredVersionsAsync(IReadOnlyCollection<StatementKey> keys)
    {
        HashSet<StatementKey> wanted = new HashSet<StatementKey>(keys);
        Dictionary<StatementKey, List<StoredVersion>> found = new Dictionary<StatementKey, List<StoredVersion>>();
        ProjectionDefinition<RecordDocument> withoutRecord = Builders<RecordDocument>.Projection.Exclude(document => document.Record);
        foreach (List<string> chunk in Chunks(IdsOf(keys)))
        {
            List<RecordDocument> documents = await _records.Find(Builders<RecordDocument>.Filter.In(document => document.Id, chunk))
                .Project<RecordDocument>(withoutRecord)
                .ToListAsync();
            AddWanted(documents, wanted, found);
        }
        return ReadOnly(found);
    }

    /// <inheritdoc/>
    public async Task<Result> AddAsync(IReadOnlyList<RecordVersion> versions)
    {
        if (versions.Count == 0)
        {
            return Result.Success();
        }
        List<RecordDocument> documents = new List<RecordDocument>();
        foreach (RecordVersion version in versions)
        {
            documents.Add(RecordDocumentMapper.ToDocument(version));
        }
        try
        {
            await _records.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = false });
            return Result.Success();
        }
        catch (MongoBulkWriteException<RecordDocument> problem) when (OnlyDuplicates(problem))
        {
            return Result.Failure(ErrorCodes.Unavailable, string.Empty, "another writer stored some of these versions first; send the batch again");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredStatement>> AsOfAsync(StatementsAsOfRequest request)
    {
        List<RecordDocument> documents = await _records.Find(AsOfFilter(request))
            .Sort(Builders<RecordDocument>.Sort.Ascending(document => document.Source).Descending(document => document.Version))
            .ToListAsync();
        List<StoredStatement> latest = new List<StoredStatement>();
        string? previousSource = null;
        foreach (RecordDocument document in documents)
        {
            if (document.Source != previousSource)
            {
                latest.Add(RecordDocumentMapper.ToStoredStatement(document));
                previousSource = document.Source;
            }
        }
        return latest;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, HashSet<string>>> EntityTypesAsync(IReadOnlyCollection<string> entityIds)
    {
        Dictionary<string, HashSet<string>> types = new Dictionary<string, HashSet<string>>();
        ProjectionDefinition<RecordDocument> idAndType = Builders<RecordDocument>.Projection.Include(document => document.Id).Include("record.type");
        foreach (List<string> chunk in Chunks(entityIds))
        {
            FilterDefinition<RecordDocument> filter = Builders<RecordDocument>.Filter.In(document => document.Id, chunk)
                & Builders<RecordDocument>.Filter.Eq(document => document.Kind, EntityKind);
            List<RecordDocument> documents = await _records.Find(filter).Project<RecordDocument>(idAndType).ToListAsync();
            foreach (RecordDocument document in documents)
            {
                AddType(types, document.Id, document.Record["type"].AsString);
            }
        }
        return types;
    }

    /// <inheritdoc/>
    public async Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds)
    {
        HashSet<string> existing = new HashSet<string>();
        foreach (List<string> chunk in Chunks(recordIds))
        {
            FilterDefinition<RecordDocument> filter = Builders<RecordDocument>.Filter.In(document => document.Id, chunk);
            List<string> found = await (await _records.DistinctAsync(document => document.Id, filter)).ToListAsync();
            existing.UnionWith(found);
        }
        return existing;
    }

    /// <summary>The documents of one id, known by the date asked and from the source asked, when they are given.</summary>
    private static FilterDefinition<RecordDocument> AsOfFilter(StatementsAsOfRequest request)
    {
        FilterDefinition<RecordDocument> filter = Builders<RecordDocument>.Filter.Eq(document => document.Id, request.Id);
        if (request.AsOf != null)
        {
            filter &= Builders<RecordDocument>.Filter.Lte(document => document.KnownAt, request.AsOf.Value);
        }
        if (request.Source != null)
        {
            filter &= Builders<RecordDocument>.Filter.Eq(document => document.Source, request.Source);
        }
        return filter;
    }

    /// <summary>Adds the stored version of each document whose statement was asked for.</summary>
    private static void AddWanted(List<RecordDocument> documents, HashSet<StatementKey> wanted, Dictionary<StatementKey, List<StoredVersion>> found)
    {
        foreach (RecordDocument document in documents)
        {
            StatementKey key = RecordDocumentMapper.ToKey(document);
            if (!wanted.Contains(key))
            {
                continue;
            }
            if (!found.TryGetValue(key, out List<StoredVersion>? versions))
            {
                versions = new List<StoredVersion>();
                found[key] = versions;
            }
            versions.Add(RecordDocumentMapper.ToStoredVersion(document));
        }
    }

    /// <summary>Adds one entity type of an id.</summary>
    private static void AddType(Dictionary<string, HashSet<string>> types, string id, string type)
    {
        if (!types.TryGetValue(id, out HashSet<string>? known))
        {
            known = new HashSet<string>();
            types[id] = known;
        }
        known.Add(type);
    }

    /// <summary>True when every failed write was a duplicate key: another writer stored that version first.</summary>
    private static bool OnlyDuplicates(MongoBulkWriteException<RecordDocument> problem)
    {
        foreach (BulkWriteError error in problem.WriteErrors)
        {
            if (error.Category != ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }
        return problem.WriteConcernError == null;
    }

    /// <summary>The distinct record ids of the statements.</summary>
    private static List<string> IdsOf(IReadOnlyCollection<StatementKey> keys)
    {
        HashSet<string> ids = new HashSet<string>();
        foreach (StatementKey key in keys)
        {
            ids.Add(key.Id);
        }
        return new List<string>(ids);
    }

    /// <summary>The ids in chunks of at most <see cref="ChunkSize"/>.</summary>
    private static List<List<string>> Chunks(IReadOnlyCollection<string> ids)
    {
        List<List<string>> chunks = new List<List<string>>();
        List<string> current = new List<string>();
        foreach (string id in ids)
        {
            current.Add(id);
            if (current.Count == ChunkSize)
            {
                chunks.Add(current);
                current = new List<string>();
            }
        }
        if (current.Count > 0)
        {
            chunks.Add(current);
        }
        return chunks;
    }

    /// <summary>The found versions as the read-only answer.</summary>
    private static IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> ReadOnly(Dictionary<StatementKey, List<StoredVersion>> found)
    {
        Dictionary<StatementKey, IReadOnlyList<StoredVersion>> answer = new Dictionary<StatementKey, IReadOnlyList<StoredVersion>>();
        foreach (KeyValuePair<StatementKey, List<StoredVersion>> entry in found)
        {
            answer[entry.Key] = entry.Value;
        }
        return answer;
    }
}
