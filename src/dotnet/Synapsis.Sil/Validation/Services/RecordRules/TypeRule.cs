using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>Rule 5: the record's type must be registered for its kind (an event type for an event, and so on).</summary>
public sealed class TypeRule : IRecordRule
{
    /// <summary>Passes, or fails with E-REGISTRY-ENTITY-TYPE, -EVENT-TYPE, -RELATION-TYPE or -CONCLUSION-TYPE.</summary>
    public Result Check(RecordContext context)
    {
        SilRecord record = context.Record;
        if (context.Registry.HasType(record.Kind, record.Type))
        {
            return Result.Success();
        }
        string kindName = record.Kind.ToString().ToLowerInvariant();
        return Result.Failure(CodeFor(record.Kind), "type", $"{kindName} type '{record.Type}' is not registered");
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
