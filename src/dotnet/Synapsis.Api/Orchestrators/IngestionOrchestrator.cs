using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Results;
using Synapsis.Api.Services;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Results;
using Synapsis.Api.Repositories;

namespace Synapsis.Api.Orchestrators;

/// <summary>
/// The ingestion process, POST /ingest: the steps in order. Each step is one call; the work is in the services and
/// repositories it calls.
/// </summary>
public sealed class IngestionOrchestrator
{
    private readonly RecordRepository _recordRepository;
    private readonly CatalogRepository _catalogRepository;
    private readonly IngestLogRepository _ingestLogRepository;

    /// <summary>Creates the process over the three repositories it writes to.</summary>
    public IngestionOrchestrator(RecordRepository recordRepository, CatalogRepository catalogRepository, IngestLogRepository ingestLogRepository)
    {
        _recordRepository = recordRepository;
        _catalogRepository = catalogRepository;
        _ingestLogRepository = ingestLogRepository;
    }

    /// <summary>Validates, stores and logs one batch; answers its summary.</summary>
    public async Task<IngestSummary> IngestAsync(IngestBatch batch)
    {
        DateTime receivedAt = DateTime.UtcNow;

        // 1. Validate: read each record as its kind, check its required fields, drop copies inside the batch.
        ValidationResult validation = RecordValidator.Validate(batch.Records);

        // 2. Enrich: the content hash and where each valid record came from.
        List<StoredRecord> candidates = RecordEnricher.Enrich(validation.Valid, batch, receivedAt);

        // 3. Find changes: compare each record with its stored states (same id, same source).
        Dictionary<string, List<StoredRecord>> storedStates = await _recordRepository.StatesByKeyAsync(candidates);
        ChangeResult changes = ChangeDetector.Detect(candidates, storedStates);

        // 4. Save to MongoDB: the new and changed records, then their sources and types in the catalog.
        await _recordRepository.AddAsync(changes.ToSave);
        await _catalogRepository.AddAsync(changes.ToSave);

        // 5. Summarize: one document per source in the ingestion log (Elasticsearch), one summary for the producer.
        await _ingestLogRepository.AddAsync(SummaryBuilder.ForEachSource(batch, receivedAt, validation, changes));
        return SummaryBuilder.ForBatch(batch, receivedAt, validation, changes);
    }
}
