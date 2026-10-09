using System;
using System.Collections.Generic;
using Synapsis.Api.Ingestion.Models.Domain;
using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Storage.Records.Models.Domain;

namespace Synapsis.Api.Ingestion.Services.Mappers;

/// <summary>Turns planned records into the versions to store, with the batch they came from.</summary>
public static class RecordVersionMapper
{
    /// <summary>A version for every planned record that stores something; unchanged records give none.</summary>
    public static List<RecordVersion> ToRecordVersions(IReadOnlyList<PlannedRecord> planned, IngestBundle bundle, DateTime receivedAt)
    {
        List<RecordVersion> versions = new List<RecordVersion>();
        foreach (PlannedRecord record in planned)
        {
            if (record.Plan.StoresSomething)
            {
                versions.Add(ToRecordVersion(record, bundle, receivedAt));
            }
        }
        return versions;
    }

    /// <summary>One planned record as a version to store.</summary>
    private static RecordVersion ToRecordVersion(PlannedRecord planned, IngestBundle bundle, DateTime receivedAt)
    {
        return new RecordVersion
        {
            Key = new StatementKey { Id = planned.Record.Record.Id, Source = planned.Record.Record.Source },
            Kind = planned.Record.Record.Kind,
            Version = planned.Plan.Version!.Value,
            KnownAt = planned.Plan.KnownAt!.Value,
            Fingerprint = planned.Record.Fingerprint,
            Record = planned.Plan.RecordToStore!.Value,
            BatchId = bundle.BatchId,
            Producer = bundle.Producer,
            ReceivedAt = receivedAt,
        };
    }
}
