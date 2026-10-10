using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Services.Registry;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Batch;

/// <summary>
/// Rule 13, references: everything a record points to must exist, stored or accepted earlier in the batch, and be of
/// a type the pointer allows: an event's participants (each in an allowed role), a relation's two ends, a
/// conclusion's subjects and premises. Records are checked in dependency order (entities, relations, events,
/// conclusions), so a record may point to one sent later in the same batch.
/// </summary>
public sealed class ReferenceRule : IBatchRule
{
    private readonly IReferenceResolver _storedRecords;

    /// <summary>Creates the rule with access to the records already stored.</summary>
    public ReferenceRule(IReferenceResolver storedRecords)
    {
        _storedRecords = storedRecords;
    }

    /// <summary>The records whose references all resolve pass; the others are refused at the first broken reference.</summary>
    public async Task<BatchRuleResult> CheckAsync(IReadOnlyList<CheckedRecord> records, IRegistry registry)
    {
        KnownRecords known = await LoadStoredAsync(records);
        List<CheckedRecord> ordered = new List<CheckedRecord>(records);
        ordered.Sort(CompareByDependency);
        List<CheckedRecord> passed = new List<CheckedRecord>();
        List<Rejection> rejected = new List<Rejection>();
        foreach (CheckedRecord candidate in ordered)
        {
            Result result = CheckRecord(candidate.Record, registry, known);
            if (!result.Succeeded)
            {
                rejected.Add(new Rejection(candidate.Index, candidate.Record.Id, result.Problem!));
                continue;
            }
            known.Add(candidate.Record.Id, EntityTypeOf(candidate.Record));
            passed.Add(candidate);
        }
        passed.Sort(CompareByIndex);
        return new BatchRuleResult(passed, rejected, 0);
    }

    /// <summary>Asks the store, once for the whole batch, about every entity and record the batch points to.</summary>
    private async Task<KnownRecords> LoadStoredAsync(IReadOnlyList<CheckedRecord> records)
    {
        HashSet<string> wantedEntities = new HashSet<string>();
        HashSet<string> wantedRecords = new HashSet<string>();
        foreach (CheckedRecord candidate in records)
        {
            ReferenceChecks.CollectReferences(candidate.Record, wantedEntities, wantedRecords);
        }
        Dictionary<string, HashSet<string>> storedTypes = new Dictionary<string, HashSet<string>>();
        if (wantedEntities.Count > 0)
        {
            storedTypes = await _storedRecords.EntityTypesAsync(wantedEntities);
        }
        HashSet<string> storedIds = new HashSet<string>();
        if (wantedRecords.Count > 0)
        {
            storedIds = await _storedRecords.ExistingAsync(wantedRecords);
        }
        return new KnownRecords(storedTypes, storedIds);
    }

    /// <summary>One record's references, by kind.</summary>
    private static Result CheckRecord(SilRecord record, IRegistry registry, KnownRecords known)
    {
        if (record is EventRecord eventRecord)
        {
            return ReferenceChecks.CheckParticipants(eventRecord, registry.FindEventType(eventRecord.Type)!, known);
        }
        if (record is RelationRecord relation)
        {
            return ReferenceChecks.CheckRelationEnds(relation, registry.FindRelationType(relation.Type)!, known);
        }
        if (record is ConclusionRecord conclusion)
        {
            return ReferenceChecks.CheckConclusion(conclusion, known);
        }
        return Result.Success();
    }

    /// <summary>The entity type of an entity record; null for the other kinds.</summary>
    private static string? EntityTypeOf(SilRecord record)
    {
        if (record is EntityRecord)
        {
            return record.Type;
        }
        return null;
    }

    /// <summary>The position of each kind in dependency order: what is pointed to comes before what points.</summary>
    private static int DependencyOrder(RecordKind kind)
    {
        switch (kind)
        {
            case RecordKind.Entity:
                return 0;
            case RecordKind.Relation:
                return 1;
            case RecordKind.Event:
                return 2;
            default:
                return 3;
        }
    }

    /// <summary>Orders records by dependency, then by batch position.</summary>
    private static int CompareByDependency(CheckedRecord first, CheckedRecord second)
    {
        int byKind = DependencyOrder(first.Record.Kind) - DependencyOrder(second.Record.Kind);
        if (byKind != 0)
        {
            return byKind;
        }
        return first.Index - second.Index;
    }

    /// <summary>Orders records by batch position.</summary>
    private static int CompareByIndex(CheckedRecord first, CheckedRecord second)
    {
        return first.Index - second.Index;
    }
}
