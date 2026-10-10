using Synapsis.Api.Services.Observe;
using Synapsis.Storage.Repositories;
using Synapsis.Sil.Services.Time;
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
namespace Synapsis.Api.Orchestrators;

/// <summary>
/// The ingestion process: read the batch → validate it against the vocabulary in force → ask the store for the
/// accepted statements' versions (one query) → plan each record's version → build the receipt → keep the new versions
/// in one write and add the batch to the ingestion log (neither on a dry run).
/// </summary>
public sealed class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly IBundleReader _reader;
    private readonly IVocabularyProvider _vocabulary;
    private readonly IBatchValidator _validator;
    private readonly IRecordRepository _records;
    private readonly IVersionPlanner _planner;
    private readonly IReceiptBuilder _receipts;
    private readonly IIngestLogRepository _log;
    private readonly TimeProvider _clock;

    /// <summary>Creates the orchestrator from the services it coordinates.</summary>
    public IngestionOrchestrator(IBundleReader reader, IVocabularyProvider vocabulary, IBatchValidator validator, IRecordRepository records, IVersionPlanner planner, IReceiptBuilder receipts, IIngestLogRepository log, TimeProvider clock)
    {
        _reader = reader;
        _vocabulary = vocabulary;
        _validator = validator;
        _records = records;
        _planner = planner;
        _receipts = receipts;
        _log = log;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<Receipt>> IngestAsync(string body, bool dryRun)
    {
        Result<IngestBundle> read = _reader.Read(body);
        if (!read.Succeeded)
        {
            return Result<Receipt>.Failure(read.Problem!);
        }
        IngestBundle bundle = read.Value!;
        DateTime receivedAt = Instant.ToMilliseconds(_clock.GetUtcNow().UtcDateTime);
        BatchValidationResult validation = await _validator.ValidateAsync(new BatchValidationRequest(bundle.Records, _vocabulary.Current, receivedAt));
        IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored = await _records.StoredVersionsAsync(_planner.StatementsOf(validation.Accepted));
        IReadOnlyList<PlannedRecord> planned = _planner.PlanBatch(validation.Accepted, stored, receivedAt);
        BatchOutcome outcome = new BatchOutcome(bundle, validation, planned, receivedAt, dryRun);
        Receipt receipt = _receipts.Build(outcome);
        if (dryRun)
        {
            return Result<Receipt>.Success(receipt);
        }
        Result added = await _records.AddAsync(RecordVersionMapper.ToRecordVersions(planned, bundle, receivedAt));
        if (!added.Succeeded)
        {
            return Result<Receipt>.Failure(added.Problem!);
        }
        await _log.AddAsync(IngestLogMapper.ToEntries(outcome, MillisecondsSince(receivedAt)));
        return Result<Receipt>.Success(receipt);
    }

    /// <summary>How many milliseconds have passed since the given moment.</summary>
    private long MillisecondsSince(DateTime start)
    {
        return (long)(_clock.GetUtcNow().UtcDateTime - start).TotalMilliseconds;
    }

}
