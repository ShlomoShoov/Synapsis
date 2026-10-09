using Synapsis.Sil.Records;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 5: the record's type must be registered for its kind (an event type for an event, and so on).
/// </summary>
public sealed class TypeRule : IRecordRule
{
    /// <summary>Returns E-REGISTRY-ENTITY-TYPE, -EVENT-TYPE, -RELATION-TYPE or -CONCLUSION-TYPE, or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        SilRecord record = context.Record;
        if (context.Registry.HasType(record.Kind, record.Type))
        {
            return null;
        }
        string kindName = record.Kind.ToString().ToLowerInvariant();
        return new RecordError(CodeFor(record.Kind), "type", $"{kindName} type '{record.Type}' is not registered");
    }

    /// <summary>The error code for an unregistered type of this kind.</summary>
    private static string CodeFor(RecordKind kind)
    {
        switch (kind)
        {
            case RecordKind.Entity:
                return ErrorCodes.RegistryEntityType;
            case RecordKind.Event:
                return ErrorCodes.RegistryEventType;
            case RecordKind.Relation:
                return ErrorCodes.RegistryRelationType;
            default:
                return ErrorCodes.RegistryConclusionType;
        }
    }
}
