using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Api.Models.Ingestion;
using Synapsis.Sil.Models.Validation;
using Synapsis.Sil.Models.Versioning;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services.Observe;

/// <summary>
/// Turns the outcome of one batch into ingestion log entries: one per source the batch's records name. Every record is
/// counted once, under its own source: stored new, stored as a new version, unchanged, refused, or an exact copy
/// dropped inside the batch (what is left after the others).
/// </summary>
public static class IngestLogMapper
{
    /// <summary>The source used when a refused record has no readable source.</summary>
    public const string UnknownSource = "unknown";

    /// <summary>At most this many refused records are kept in detail per entry.</summary>
    public const int MaxDetailedRejections = 50;

    /// <summary>The log entries of one batch, in the order their sources first appear in it.</summary>
    public static List<IngestLogEntry> ToEntries(BatchOutcome outcome, long durationMs)
    {
        Dictionary<string, IngestLogEntry> bySource = new Dictionary<string, IngestLogEntry>();
        List<IngestLogEntry> entries = new List<IngestLogEntry>();
        foreach (JsonElement record in outcome.Bundle.Records)
        {
            IngestLogEntry entry = EntryOf(SourceOf(record), outcome, durationMs, bySource, entries);
            entry.Received++;
        }
        foreach (PlannedRecord planned in outcome.Planned)
        {
            CountPlan(bySource[planned.Record.Record.Source], planned.Plan.Outcome);
        }
        foreach (Rejection rejection in outcome.Validation.Rejected)
        {
            CountRejection(bySource[SourceOf(outcome.Bundle.Records[rejection.Index])], rejection);
        }
        foreach (IngestLogEntry entry in entries)
        {
            entry.Duplicates = entry.Received - entry.New - entry.Changed - entry.Unchanged - entry.Rejected;
        }
        return entries;
    }

    /// <summary>The entry of a source, created the first time the source appears.</summary>
    private static IngestLogEntry EntryOf(string source, BatchOutcome outcome, long durationMs, Dictionary<string, IngestLogEntry> bySource, List<IngestLogEntry> entries)
    {
        if (bySource.TryGetValue(source, out IngestLogEntry? existing))
        {
            return existing;
        }
        IngestLogEntry entry = new IngestLogEntry
        {
            Timestamp = outcome.ReceivedAt,
            BatchId = outcome.Bundle.BatchId,
            Producer = outcome.Bundle.Producer,
            Source = source,
            DurationMs = durationMs,
        };
        bySource[source] = entry;
        entries.Add(entry);
        return entry;
    }

    /// <summary>Counts one accepted record by what its version plan decided.</summary>
    private static void CountPlan(IngestLogEntry entry, VersionOutcome outcome)
    {
        if (outcome == VersionOutcome.New)
        {
            entry.New++;
        }
        else if (outcome == VersionOutcome.NewVersion)
        {
            entry.Changed++;
        }
        else
        {
            entry.Unchanged++;
        }
    }

    /// <summary>Counts one refused record under its error code, and keeps its details while there is room.</summary>
    private static void CountRejection(IngestLogEntry entry, Rejection rejection)
    {
        entry.Rejected++;
        RejectionCount? count = entry.RejectionCounts.Find(existing => existing.Code == rejection.Problem.Code);
        if (count == null)
        {
            count = new RejectionCount { Code = rejection.Problem.Code };
            entry.RejectionCounts.Add(count);
        }
        count.Count++;
        if (entry.Rejections.Count < MaxDetailedRejections)
        {
            entry.Rejections.Add(new LoggedRejection { Index = rejection.Index, Id = rejection.Id, Code = rejection.Problem.Code, Path = rejection.Problem.Path });
        }
    }

    /// <summary>The "source" a raw record names, or "unknown" when it has none that can be read.</summary>
    private static string SourceOf(JsonElement record)
    {
        if (record.ValueKind == JsonValueKind.Object && record.TryGetProperty("source", out JsonElement source) && source.ValueKind == JsonValueKind.String)
        {
            return source.GetString()!;
        }
        return UnknownSource;
    }
}
