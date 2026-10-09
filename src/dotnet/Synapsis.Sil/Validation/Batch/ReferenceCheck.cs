using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Records;
using Synapsis.Sil.Records.Parts;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// Rule 13, second part: every record a record points to must exist, either stored or accepted earlier in this
/// batch, and must be of a type the pointer allows: an event's participants (each in an allowed role), a relation's
/// two ends, a conclusion's subjects and premises. Records are checked in dependency order (entities, relations,
/// events, conclusions), and each accepted record becomes available to the ones after it.
/// </summary>
public sealed class ReferenceCheck
{
    /// <summary>Subject keys ("subject:h3:...", "subject:admin:US-CA") name things that are not records.</summary>
    private const string SubjectKeyPrefix = "subject:";

    private readonly RegistrySnapshot _registry;
    private readonly Dictionary<string, HashSet<string>> _storedEntityTypes;
    private readonly HashSet<string> _storedIds;
    private readonly Dictionary<string, HashSet<string>> _batchEntityTypes = new Dictionary<string, HashSet<string>>();
    private readonly HashSet<string> _batchIds = new HashSet<string>();

    /// <summary>Creates the check with what is stored (asked once for the whole batch).</summary>
    private ReferenceCheck(RegistrySnapshot registry, Dictionary<string, HashSet<string>> storedEntityTypes,
        HashSet<string> storedIds)
    {
        _registry = registry;
        _storedEntityTypes = storedEntityTypes;
        _storedIds = storedIds;
    }

    /// <summary>
    /// Asks the store, once for the whole batch, about every entity and record the batch points to, and returns the
    /// check ready to use.
    /// </summary>
    public static async Task<ReferenceCheck> CreateAsync(List<PreparedRecord> records, RegistrySnapshot registry,
        IReferenceResolver resolver)
    {
        HashSet<string> wantedEntities = new HashSet<string>();
        HashSet<string> wantedRecords = new HashSet<string>();
        foreach (PreparedRecord prepared in records)
        {
            CollectReferences(prepared.Record, wantedEntities, wantedRecords);
        }
        Dictionary<string, HashSet<string>> storedTypes = new Dictionary<string, HashSet<string>>();
        if (wantedEntities.Count > 0)
        {
            storedTypes = await resolver.EntityTypesAsync(wantedEntities);
        }
        HashSet<string> storedIds = new HashSet<string>();
        if (wantedRecords.Count > 0)
        {
            storedIds = await resolver.ExistingAsync(wantedRecords);
        }
        return new ReferenceCheck(registry, storedTypes, storedIds);
    }

    /// <summary>The position of each kind in dependency order: what is pointed to comes before what points.</summary>
    public static int DependencyOrder(RecordKind kind)
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

    /// <summary>Checks one record's references; returns the first problem, or null.</summary>
    public RecordError? Check(SilRecord record)
    {
        if (record is EventRecord eventRecord)
        {
            return CheckParticipants(eventRecord);
        }
        if (record is RelationRecord relation)
        {
            return CheckRelationEnds(relation);
        }
        if (record is ConclusionRecord conclusion)
        {
            return CheckConclusion(conclusion);
        }
        return null;
    }

    /// <summary>Makes an accepted record available to the records checked after it.</summary>
    public void Accept(SilRecord record)
    {
        _batchIds.Add(record.Id);
        if (record is EntityRecord)
        {
            if (!_batchEntityTypes.TryGetValue(record.Id, out HashSet<string>? types))
            {
                types = new HashSet<string>();
                _batchEntityTypes[record.Id] = types;
            }
            types.Add(record.Type);
        }
    }

    /// <summary>Each participant: a role the event type allows, an existing entity, of a type the role takes.</summary>
    private RecordError? CheckParticipants(EventRecord eventRecord)
    {
        if (eventRecord.Participants == null)
        {
            return null;
        }
        EventTypeDefinition type = _registry.EventTypes[eventRecord.Type];
        for (int index = 0; index < eventRecord.Participants.Count; index++)
        {
            Participant participant = eventRecord.Participants[index];
            if (!type.Roles.TryGetValue(participant.Role, out List<string>? allowedTypes))
            {
                return new RecordError(ErrorCodes.Role, $"participants[{index}].role",
                    $"role '{participant.Role}' is not allowed for '{eventRecord.Type}'");
            }
            HashSet<string> entityTypes = TypesOf(participant.Entity);
            if (entityTypes.Count == 0)
            {
                return new RecordError(ErrorCodes.RefMissing, $"participants[{index}].entity",
                    $"entity '{participant.Entity}' is neither in the batch nor stored");
            }
            if (!Overlaps(entityTypes, allowedTypes))
            {
                return new RecordError(ErrorCodes.Role, $"participants[{index}].entity",
                    $"role '{participant.Role}' takes {string.Join(", ", allowedTypes)}, not {string.Join(", ", entityTypes)}");
            }
        }
        return null;
    }

    /// <summary>Both ends of a relation: existing entities, of types the relation type allows at that end.</summary>
    private RecordError? CheckRelationEnds(RelationRecord relation)
    {
        RelationTypeDefinition type = _registry.RelationTypes[relation.Type];
        RecordError? error = CheckEnd("from", relation.From, type.From, relation.Type);
        if (error != null)
        {
            return error;
        }
        return CheckEnd("to", relation.To, type.To, relation.Type);
    }

    /// <summary>One end of a relation.</summary>
    private RecordError? CheckEnd(string end, string entityId, List<string> allowedTypes, string relationType)
    {
        HashSet<string> entityTypes = TypesOf(entityId);
        if (entityTypes.Count == 0)
        {
            return new RecordError(ErrorCodes.RefMissing, end, $"entity '{entityId}' is neither in the batch nor stored");
        }
        if (!Overlaps(entityTypes, allowedTypes))
        {
            return new RecordError(ErrorCodes.RelationDomain, end,
                $"'{relationType}' {end} takes {string.Join(", ", allowedTypes)}, not {string.Join(", ", entityTypes)}");
        }
        return null;
    }

    /// <summary>A conclusion's subjects (except subject keys) and premises must exist.</summary>
    private RecordError? CheckConclusion(ConclusionRecord conclusion)
    {
        for (int index = 0; index < conclusion.About.Count; index++)
        {
            string subject = conclusion.About[index];
            if (!subject.StartsWith(SubjectKeyPrefix) && !Exists(subject))
            {
                return new RecordError(ErrorCodes.RefMissing, $"about[{index}]", $"'{subject}' is neither in the batch nor stored");
            }
        }
        for (int index = 0; index < conclusion.Premises.Count; index++)
        {
            string premise = conclusion.Premises[index];
            if (!Exists(premise))
            {
                return new RecordError(ErrorCodes.PremiseMissing, $"premises[{index}]",
                    $"premise '{premise}' is neither in the batch nor stored");
            }
        }
        return null;
    }

    /// <summary>True when the record is accepted earlier in the batch or stored.</summary>
    private bool Exists(string recordId)
    {
        return _batchIds.Contains(recordId) || _storedIds.Contains(recordId);
    }

    /// <summary>The types an entity has, from the batch and from the store together; empty when it exists nowhere.</summary>
    private HashSet<string> TypesOf(string entityId)
    {
        HashSet<string> types = new HashSet<string>();
        if (_batchEntityTypes.TryGetValue(entityId, out HashSet<string>? batchTypes))
        {
            types.UnionWith(batchTypes);
        }
        if (_storedEntityTypes.TryGetValue(entityId, out HashSet<string>? storedTypes))
        {
            types.UnionWith(storedTypes);
        }
        return types;
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
}
