using System;
using System.Collections.Generic;
using Synapsis.Api.Models;
using Synapsis.Api.Models.Records;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 5 of ingestion, the summary: counts what happened to every record of the batch. One summary for the whole
/// batch (the answer to the producer), and one per source for the ingestion log (the UI filters by source).
/// </summary>
public static class SummaryBuilder
{
    /// <summary>How many refused records a log document keeps (the answer keeps all of them).</summary>
    public const int LoggedRejectionsLimit = 50;

    /// <summary>The summary of the whole batch, with every refused record.</summary>
    public static IngestSummary ForBatch(IngestBatch batch, DateTime receivedAt, ValidationResult validation, ChangeResult changes)
    {
        IngestSummary summary = NewSummary(batch, receivedAt, null);
        Count(summary, validation, changes, int.MaxValue);
        return summary;
    }

    /// <summary>One summary per source named in the batch, sorted by source, with at most 50 refused records each.</summary>
    public static List<IngestSummary> ForEachSource(IngestBatch batch, DateTime receivedAt, ValidationResult validation, ChangeResult changes)
    {
        List<IngestSummary> summaries = new List<IngestSummary>();
        foreach (string source in SourcesOf(validation, changes))
        {
            IngestSummary summary = NewSummary(batch, receivedAt, source);
            Count(summary, validation, changes, LoggedRejectionsLimit);
            summaries.Add(summary);
        }
        return summaries;
    }

    /// <summary>An empty summary of the batch; the duration is the time since the batch was received.</summary>
    private static IngestSummary NewSummary(IngestBatch batch, DateTime receivedAt, string? source)
    {
        IngestSummary summary = new IngestSummary();
        summary.Timestamp = receivedAt;
        summary.BatchId = batch.BatchId;
        summary.Producer = batch.Producer;
        summary.Source = source;
        summary.DurationMs = (long)(DateTime.UtcNow - receivedAt).TotalMilliseconds;
        return summary;
    }

    /// <summary>
    /// Fills the counts of the summary's source (every source when it has none), and its refused records up to the
    /// limit.
    /// </summary>
    private static void Count(IngestSummary summary, ValidationResult validation, ChangeResult changes, int rejectionsLimit)
    {
        CountStored(summary, changes);
        foreach (SilRecord duplicate in validation.Duplicates)
        {
            if (IsCounted(summary, duplicate.Source!))
            {
                summary.Duplicates++;
            }
        }
        CountRejected(summary, validation.Rejected, rejectionsLimit);
        summary.Received = summary.New + summary.Changed + summary.Unchanged + summary.Duplicates + summary.Rejected;
    }

    /// <summary>Counts the stored records (state 1 means new, a higher state means changed) and the unchanged ones.</summary>
    private static void CountStored(IngestSummary summary, ChangeResult changes)
    {
        foreach (StoredRecord saved in changes.ToSave)
        {
            if (!IsCounted(summary, saved.Source))
            {
                continue;
            }
            if (saved.State == 1)
            {
                summary.New++;
            }
            else
            {
                summary.Changed++;
            }
        }
        foreach (StoredRecord unchanged in changes.Unchanged)
        {
            if (IsCounted(summary, unchanged.Source))
            {
                summary.Unchanged++;
            }
        }
    }

    /// <summary>Counts the refused records, per code too, and lists them up to the limit.</summary>
    private static void CountRejected(IngestSummary summary, List<RejectedRecord> rejectedRecords, int rejectionsLimit)
    {
        foreach (RejectedRecord rejected in rejectedRecords)
        {
            if (!IsCounted(summary, rejected.Source))
            {
                continue;
            }
            summary.Rejected++;
            AddToCodeCount(summary.RejectionCounts, rejected.Code);
            if (summary.Rejections.Count < rejectionsLimit)
            {
                summary.Rejections.Add(rejected);
            }
        }
    }

    /// <summary>True when a record of this source belongs in the summary (a summary without a source takes every source).</summary>
    private static bool IsCounted(IngestSummary summary, string source)
    {
        return summary.Source == null || summary.Source == source;
    }

    /// <summary>Adds one to the code's count, creating the count the first time the code appears.</summary>
    private static void AddToCodeCount(List<CodeCount> codeCounts, string code)
    {
        foreach (CodeCount codeCount in codeCounts)
        {
            if (codeCount.Code == code)
            {
                codeCount.Count++;
                return;
            }
        }
        codeCounts.Add(new CodeCount { Code = code, Count = 1 });
    }

    /// <summary>Every source named in the batch (records stored, unchanged, copied or refused), sorted.</summary>
    private static List<string> SourcesOf(ValidationResult validation, ChangeResult changes)
    {
        SortedSet<string> sources = new SortedSet<string>();
        foreach (StoredRecord saved in changes.ToSave)
        {
            sources.Add(saved.Source);
        }
        foreach (StoredRecord unchanged in changes.Unchanged)
        {
            sources.Add(unchanged.Source);
        }
        foreach (SilRecord duplicate in validation.Duplicates)
        {
            sources.Add(duplicate.Source!);
        }
        foreach (RejectedRecord rejected in validation.Rejected)
        {
            sources.Add(rejected.Source);
        }
        return new List<string>(sources);
    }
}
