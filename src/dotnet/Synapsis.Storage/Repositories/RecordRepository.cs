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
        foreach (List<string> chunk in IdChunks.Chunks(IdChunks.IdsOf(keys)))
        {
            List<RecordDocument> documents = await _records.Find(Builders<RecordDocument>.Filter.In(document => document.Id, chunk))
                .Project<RecordDocument>(withoutRecord)
                .ToListAsync();
            RecordAnswers.AddWanted(documents, wanted, found);
        }
        return RecordAnswers.ReadOnly(found);
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
        foreach (List<string> chunk in IdChunks.Chunks(entityIds))
        {
            FilterDefinition<RecordDocument> filter = Builders<RecordDocument>.Filter.In(document => document.Id, chunk)
                & Builders<RecordDocument>.Filter.Eq(document => document.Kind, EntityKind);
            List<RecordDocument> documents = await _records.Find(filter).Project<RecordDocument>(idAndType).ToListAsync();
            foreach (RecordDocument document in documents)
            {
                RecordAnswers.AddType(types, document.Id, document.Record["type"].AsString);
            }
        }
        return types;
    }

    /// <inheritdoc/>
    public async Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds)
    {
        HashSet<string> existing = new HashSet<string>();
        foreach (List<string> chunk in IdChunks.Chunks(recordIds))
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

}
