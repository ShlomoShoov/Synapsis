using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Api.Services.Ingestion;
using Synapsis.Api.Models.Ingestion;
using Synapsis.Api.Services.Vocabulary;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Services.Reading;
using Synapsis.Sil.Orchestrators;
using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Services.Versioning;
using Synapsis.Sil.Models.Versioning;
using Synapsis.Storage.Repositories;
namespace Synapsis.Api.Orchestrators;

/// <summary>
/// The ingestion process: read the batch → validate it against the vocabulary in force → ask the store for the
/// accepted statements' versions (one query) → plan each record's version → build the receipt → keep the new versions
/// in one write (not on a dry run).
/// </summary>
public sealed class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly IBundleReader _reader;
    private readonly IVocabularyProvider _vocabulary;
    private readonly IBatchValidator _validator;
    private readonly IRecordRepository _records;
    private readonly IVersionPlanner _planner;
    private readonly IReceiptBuilder _receipts;
    private readonly TimeProvider _clock;

    /// <summary>Creates the orchestrator from the services it coordinates.</summary>
    public IngestionOrchestrator(IBundleReader reader, IVocabularyProvider vocabulary, IBatchValidator validator, IRecordRepository records, IVersionPlanner planner, IReceiptBuilder receipts, TimeProvider clock)
    {
        _reader = reader;
        _vocabulary = vocabulary;
        _validator = validator;
        _records = records;
        _planner = planner;
        _receipts = receipts;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<Receipt>> IngestAsync(IngestionRequest request)
    {
        Result<IngestBundle> read = _reader.Read(request.Body);
        if (!read.Succeeded)
        {
            return Result<Receipt>.Failure(read.Problem!);
        }
        IngestBundle bundle = read.Value!;
        DateTime receivedAt = ReceiveTime();
        BatchValidationResult validation = await _validator.ValidateAsync(new BatchValidationRequest(bundle.Records, _vocabulary.Current, receivedAt));
        List<PlannedRecord> planned = await PlanAsync(validation.Accepted, receivedAt);
        Receipt receipt = _receipts.Build(new ReceiptRequest(bundle, validation, planned, receivedAt, request.DryRun));
        if (request.DryRun)
        {
            return Result<Receipt>.Success(receipt);
        }
        Result added = await _records.AddAsync(RecordVersionMapper.ToRecordVersions(planned, bundle, receivedAt));
        if (!added.Succeeded)
        {
            return Result<Receipt>.Failure(added.Problem!);
        }
        return Result<Receipt>.Success(receipt);
    }

    /// <summary>Asks the store for the accepted statements' versions once, then plans each record.</summary>
    private async Task<List<PlannedRecord>> PlanAsync(IReadOnlyList<CheckedRecord> accepted, DateTime receivedAt)
    {
        IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored = await _records.StoredVersionsAsync(KeysOf(accepted));
        List<PlannedRecord> planned = new List<PlannedRecord>();
        foreach (CheckedRecord record in accepted)
        {
            IReadOnlyList<StoredVersion> versions = StoredOf(stored, KeyOf(record));
            planned.Add(new PlannedRecord(record, _planner.Plan(new VersionPlanRequest(record, versions, receivedAt))));
        }
        return planned;
    }

    /// <summary>Now, to the millisecond: MongoDB keeps dates to the millisecond, so the stored and the written time agree.</summary>
    private DateTime ReceiveTime()
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    /// <summary>The statement of each accepted record.</summary>
    private static List<StatementKey> KeysOf(IReadOnlyList<CheckedRecord> accepted)
    {
        List<StatementKey> keys = new List<StatementKey>();
        foreach (CheckedRecord record in accepted)
        {
            keys.Add(KeyOf(record));
        }
        return keys;
    }

    /// <summary>The statement a record belongs to.</summary>
    private static StatementKey KeyOf(CheckedRecord record)
    {
        return new StatementKey { Id = record.Record.Id, Source = record.Record.Source };
    }

    /// <summary>The stored versions of one statement; none when it is new.</summary>
    private static IReadOnlyList<StoredVersion> StoredOf(IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored, StatementKey key)
    {
        if (stored.TryGetValue(key, out IReadOnlyList<StoredVersion>? versions))
        {
            return versions;
        }
        return Array.Empty<StoredVersion>();
    }
}
