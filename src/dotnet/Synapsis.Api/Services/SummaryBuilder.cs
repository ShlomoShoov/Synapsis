using System;
using System.Collections.Generic;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Models.Results;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Models.Results;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 5 of ingestion, the summary: what happened to every record, counted in groups. One group for the whole batch
/// (the answer to the producer), or one group per source (the ingestion log; the UI filters by source).
/// </summary>
public static class SummaryBuilder
{
    /// <summary>How many refused records a log document keeps (the answer keeps all of them).</summary>
    public const int LoggedRejectionsLimit = 50;

    /// <summary>The key of the one group that holds the whole batch.</summary>
    private const string WholeBatch = "whole batch";

    /// <summary>The summary of the whole batch, with every refused record.</summary>
    public static IngestSummary ForBatch(IngestBatch batch, DateTime receivedAt, ValidationResult validation, ChangeResult changes)
    {
        SortedDictionary<string, IngestSummary> groups = Group(validation, changes, false);
        IngestSummary whole = new IngestSummary();
        if (groups.ContainsKey(WholeBatch))
        {
            whole = groups[WholeBatch];
        }
        Finish(whole, batch, receivedAt);
        return whole;
    }

    /// <summary>One summary per source named in the batch, sorted by source, with at most 50 refused records each.</summary>
    public static List<IngestSummary> ForEachSource(IngestBatch batch, DateTime receivedAt, ValidationResult validation, ChangeResult changes)
    {
        List<IngestSummary> summaries = new List<IngestSummary>();
        foreach (IngestSummary group in Group(validation, changes, true).Values)
        {
            if (group.Rejections.Count > LoggedRejectionsLimit)
            {
                group.Rejections = group.Rejections.GetRange(0, LoggedRejectionsLimit);
            }
            Finish(group, batch, receivedAt);
            summaries.Add(group);
        }
        return summaries;
    }

    /// <summary>Puts every record in its group (its source, or one group for all) and counts what happened to it.</summary>
    private static SortedDictionary<string, IngestSummary> Group(ValidationResult validation, ChangeResult changes, bool bySource)
    {
        SortedDictionary<string, IngestSummary> groups = new SortedDictionary<string, IngestSummary>();

        // 1. Stored records: state 1 is new, a higher state is changed.
        foreach (StoredRecord saved in changes.ToSave)
        {
            IngestSummary group = GroupOf(groups, saved.Source, bySource);
            if (saved.State == 1)
            {
                group.New++;
            }
            else
            {
                group.Changed++;
            }
        }

        // 2. Unchanged records and exact copies inside the batch.
        foreach (StoredRecord unchanged in changes.Unchanged)
        {
            GroupOf(groups, unchanged.Source, bySource).Unchanged++;
        }
        foreach (SilRecord duplicate in validation.Duplicates)
        {
            GroupOf(groups, duplicate.Source!, bySource).Duplicates++;
        }

        // 3. Refused records: listed, and counted per code.
        foreach (RejectedRecord rejected in validation.Rejected)
        {
            IngestSummary group = GroupOf(groups, rejected.Source, bySource);
            group.Rejected++;
            group.Rejections.Add(rejected);
            CountCode(group.RejectionCounts, rejected.Code);
        }
        return groups;
    }

    /// <summary>The group of a source (or the one group of the whole batch), created the first time it is needed.</summary>
    private static IngestSummary GroupOf(SortedDictionary<string, IngestSummary> groups, string source, bool bySource)
    {
        string key = bySource ? source : WholeBatch;
        if (!groups.ContainsKey(key))
        {
            groups.Add(key, new IngestSummary { Source = bySource ? source : null });
        }
        return groups[key];
    }

    /// <summary>Adds one to the code's count, creating the count the first time the code appears.</summary>
    private static void CountCode(List<CodeCount> codeCounts, string code)
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

    /// <summary>Adds what the batch says (id, producer, times) and the total received.</summary>
    private static void Finish(IngestSummary summary, IngestBatch batch, DateTime receivedAt)
    {
        summary.Timestamp = receivedAt;
        summary.BatchId = batch.BatchId;
        summary.Producer = batch.Producer;
        summary.DurationMs = (long)(DateTime.UtcNow - receivedAt).TotalMilliseconds;
        summary.Received = summary.New + summary.Changed + summary.Unchanged + summary.Duplicates + summary.Rejected;
    }
}
