using System.Collections.Generic;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Models.Vocabulary.Types;

namespace Synapsis.Sil.Services.Rules.Batch;

/// <summary>
/// The checks of rule 13 for one record: an event's participants, a relation's two ends, a conclusion's subjects and
/// premises; and the list of what a record points to. Used by <see cref="ReferenceRule"/>.
/// </summary>
public static class ReferenceChecks
{
    /// <summary>Subject keys ("subject:h3:...", "subject:admin:US-CA") name things that are not records.</summary>
    public const string SubjectKeyPrefix = "subject:";

    /// <summary>Each participant: a role the event type allows, an existing entity, of a type the role takes.</summary>
    public static Result CheckParticipants(EventRecord eventRecord, EventTypeDefinition type, KnownRecords known)
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
    public static Result CheckRelationEnds(RelationRecord relation, RelationTypeDefinition type, KnownRecords known)
    {
        Result from = CheckEnd("from", relation.From, type.From, relation.Type, known);
        if (!from.Succeeded)
        {
            return from;
        }
        return CheckEnd("to", relation.To, type.To, relation.Type, known);
    }

    /// <summary>One end of a relation.</summary>
    public static Result CheckEnd(string end, string entityId, List<string> allowedTypes, string relationType, KnownRecords known)
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
    public static Result CheckConclusion(ConclusionRecord conclusion, KnownRecords known)
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

    /// <summary>True when at least one of the entity's types is allowed.</summary>
    public static bool Overlaps(HashSet<string> entityTypes, List<string> allowedTypes)
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
    public static void CollectReferences(SilRecord record, HashSet<string> entities, HashSet<string> records)
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
