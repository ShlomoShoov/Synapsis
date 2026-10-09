using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Synapsis.Sil.Fingerprints;
using Synapsis.Sil.Records;
using Synapsis.Sil.Registry;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// Checks a whole batch: every record on its own (rules 1 to 12), then duplicates, then references in dependency
/// order (rule 13). Returns which records are accepted and which are refused, each refusal with its code and path.
/// </summary>
public static class BatchValidator
{
    /// <summary>Checks the batch's raw records against the registry, asking the resolver about stored records.</summary>
    public static async Task<BatchResult> ValidateAsync(IReadOnlyList<JsonElement> records, RegistrySnapshot registry,
        IReferenceResolver resolver, DateTime receivedAt)
    {
        List<Rejection> rejected = new List<Rejection>();
        List<PreparedRecord> prepared = CheckEachRecord(records, registry, receivedAt, rejected);
        List<PreparedRecord> unique = DuplicateCheck.KeepFirstCopies(prepared, rejected, out int duplicates);
        List<PreparedRecord> accepted = await CheckReferencesAsync(unique, registry, resolver, rejected);
        accepted.Sort(CompareByIndex);
        rejected.Sort(CompareByIndex);
        return new BatchResult(accepted, rejected, duplicates);
    }

    /// <summary>Rules 1 to 12 for every record; returns the records that pass, with their fingerprints.</summary>
    private static List<PreparedRecord> CheckEachRecord(IReadOnlyList<JsonElement> records, RegistrySnapshot registry,
        DateTime receivedAt, List<Rejection> rejected)
    {
        List<PreparedRecord> prepared = new List<PreparedRecord>();
        for (int index = 0; index < records.Count; index++)
        {
            JsonElement raw = records[index];
            RecordError? error = RecordValidator.Check(raw, registry, receivedAt, out SilRecord? record);
            if (error != null)
            {
                rejected.Add(new Rejection(index, IdOf(raw), error));
                continue;
            }
            prepared.Add(new PreparedRecord(index, raw, record!, ContentFingerprint.OfRecord(raw)));
        }
        return prepared;
    }

    /// <summary>Rule 13's references, in dependency order; returns the records that pass.</summary>
    private static async Task<List<PreparedRecord>> CheckReferencesAsync(List<PreparedRecord> unique,
        RegistrySnapshot registry, IReferenceResolver resolver, List<Rejection> rejected)
    {
        ReferenceCheck references = await ReferenceCheck.CreateAsync(unique, registry, resolver);
        List<PreparedRecord> ordered = new List<PreparedRecord>(unique);
        ordered.Sort(CompareByDependency);
        List<PreparedRecord> accepted = new List<PreparedRecord>();
        foreach (PreparedRecord prepared in ordered)
        {
            RecordError? error = references.Check(prepared.Record);
            if (error != null)
            {
                rejected.Add(new Rejection(prepared.Index, prepared.Record.Id, error));
                continue;
            }
            references.Accept(prepared.Record);
            accepted.Add(prepared);
        }
        return accepted;
    }

    /// <summary>The record's "id" when it has one as text (a refused record may not).</summary>
    private static string? IdOf(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("id", out JsonElement id) &&
            id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }
        return null;
    }

    /// <summary>Orders records by dependency (entities first, conclusions last), then by batch position.</summary>
    private static int CompareByDependency(PreparedRecord first, PreparedRecord second)
    {
        int byKind = ReferenceCheck.DependencyOrder(first.Record.Kind) - ReferenceCheck.DependencyOrder(second.Record.Kind);
        if (byKind != 0)
        {
            return byKind;
        }
        return first.Index - second.Index;
    }

    /// <summary>Orders prepared records by batch position.</summary>
    private static int CompareByIndex(PreparedRecord first, PreparedRecord second)
    {
        return first.Index - second.Index;
    }

    /// <summary>Orders rejections by batch position.</summary>
    private static int CompareByIndex(Rejection first, Rejection second)
    {
        return first.Index - second.Index;
    }
}
