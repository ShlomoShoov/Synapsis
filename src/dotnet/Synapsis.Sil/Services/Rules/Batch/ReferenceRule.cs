using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Models.Vocabulary.Types;
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
    /// <summary>Subject keys ("subject:h3:...", "subject:admin:US-CA") name things that are not records.</summary>
    private const string SubjectKeyPrefix = "subject:";

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
            CollectReferences(candidate.Record, wantedEntities, wantedRecords);
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
            return CheckParticipants(eventRecord, registry.FindEventType(eventRecord.Type)!, known);
        }
        if (record is RelationRecord relation)
        {
            return CheckRelationEnds(relation, registry.FindRelationType(relation.Type)!, known);
        }
        if (record is ConclusionRecord conclusion)
        {
            return CheckConclusion(conclusion, known);
        }
        return Result.Success();
    }

    /// <summary>Each participant: a role the event type allows, an existing entity, of a type the role takes.</summary>
    private static Result CheckParticipants(EventRecord eventRecord, EventTypeDefinition type, KnownRecords known)
    {
        if (eventRecord.Participants == null)
        {
            return Result.Success();
        }
        for (int index = 0; index < eventRecord.Participants.Count; index++)
        {
            Participant participant = eventRecord.Participants[index];
            if (!type.Roles.TryGetValue(participant.Role, out List<string>? allowedTypes))
            {
                return Result.Failure(ErrorCodes.Role, $"participants[{index}].role", $"role '{participant.Role}' is not allowed for '{eventRecord.Type}'");
            }
            HashSet<string> entityTypes = known.TypesOf(participant.Entity);
            if (entityTypes.Count == 0)
            {
                return Result.Failure(ErrorCodes.RefMissing, $"participants[{index}].entity", $"entity '{participant.Entity}' is neither in the batch nor stored");
            }
            if (!Overlaps(entityTypes, allowedTypes))
            {
                return Result.Failure(ErrorCodes.Role, $"participants[{index}].entity", $"role '{participant.Role}' takes {string.Join(", ", allowedTypes)}, not {string.Join(", ", entityTypes)}");
            }
        }
        return Result.Success();
    }

    /// <summary>Both ends of a relation: existing entities, of types the relation type allows at that end.</summary>
    private static Result CheckRelationEnds(RelationRecord relation, RelationTypeDefinition type, KnownRecords known)
    {
        Result from = CheckEnd("from", relation.From, type.From, relation.Type, known);
        if (!from.Succeeded)
        {
            return from;
        }
        return CheckEnd("to", relation.To, type.To, relation.Type, known);
    }

    /// <summary>One end of a relation.</summary>
    private static Result CheckEnd(string end, string entityId, List<string> allowedTypes, string relationType, KnownRecords known)
    {
        HashSet<string> entityTypes = known.TypesOf(entityId);
        if (entityTypes.Count == 0)
        {
            return Result.Failure(ErrorCodes.RefMissing, end, $"entity '{entityId}' is neither in the batch nor stored");
        }
        if (!Overlaps(entityTypes, allowedTypes))
        {
            return Result.Failure(ErrorCodes.RelationDomain, end, $"'{relationType}' {end} takes {string.Join(", ", allowedTypes)}, not {string.Join(", ", entityTypes)}");
        }
        return Result.Success();
    }

    /// <summary>A conclusion's subjects (except subject keys) and premises must exist.</summary>
    private static Result CheckConclusion(ConclusionRecord conclusion, KnownRecords known)
    {
        for (int index = 0; index < conclusion.About.Count; index++)
        {
            string subject = conclusion.About[index];
            if (!subject.StartsWith(SubjectKeyPrefix) && !known.Exists(subject))
            {
                return Result.Failure(ErrorCodes.RefMissing, $"about[{index}]", $"'{subject}' is neither in the batch nor stored");
            }
        }
        for (int index = 0; index < conclusion.Premises.Count; index++)
        {
            if (!known.Exists(conclusion.Premises[index]))
            {
                return Result.Failure(ErrorCodes.PremiseMissing, $"premises[{index}]", $"premise '{conclusion.Premises[index]}' is neither in the batch nor stored");
            }
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

    /// <summary>True when at least one of the entity's types is allowed.</summary>
    private static bool Overlaps(HashSet<string> entityTypes, List<string> allowedTypes)
    {
        foreach (string allowed in allowedTypes)
        {
            if (entityTypes.Contains(allowed))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Adds the entities and records a record points to.</summary>
    private static void CollectReferences(SilRecord record, HashSet<string> entities, HashSet<string> records)
    {
        if (record is EventRecord eventRecord && eventRecord.Participants != null)
        {
            foreach (Participant participant in eventRecord.Participants)
            {
                entities.Add(participant.Entity);
            }
        }
        else if (record is RelationRecord relation)
        {
            entities.Add(relation.From);
            entities.Add(relation.To);
        }
        else if (record is ConclusionRecord conclusion)
        {
            foreach (string subject in conclusion.About)
            {
                if (!subject.StartsWith(SubjectKeyPrefix))
                {
                    records.Add(subject);
                }
            }
            records.UnionWith(conclusion.Premises);
        }
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
