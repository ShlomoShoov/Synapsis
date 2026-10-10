using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Services.Registry;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Record;

/// <summary>
/// Rule 4: an id is "prefix:namespace:key". The prefix must match the kind (ent, evt, rel, con), and the namespace
/// must be owned by the source's package, or, for an entity, be a shared namespace (such as cve) whose key format
/// and entity type it respects. This keeps ids from different feeders from colliding.
/// </summary>
public sealed class IdentityRule : IRecordRule
{
    /// <summary>Passes, or fails with E-ID-NAMESPACE.</summary>
    public Result Check(RecordContext context)
    {
        SilRecord record = context.Record;
        string[] parts = record.Id.Split(':', 3);
        string expectedPrefix = PrefixOf(record.Kind);
        if (parts[0] != expectedPrefix)
        {
            return Failure($"a {record.Kind.ToString().ToLowerInvariant()} id starts with '{expectedPrefix}:'");
        }
        string package = context.Registry.PackageOfSource(record.Source)!;
        if (record.Kind == RecordKind.Entity)
        {
            return CheckEntityNamespace(context.Registry, record, parts[1], parts[2], package);
        }
        if (context.Registry.OwnerOfRecordNamespace(parts[1]) != package)
        {
            return Failure($"namespace '{parts[1]}' is not owned by package '{package}' (source {record.Source})");
        }
        return Result.Success();
    }

    /// <summary>An entity id: a shared namespace with its key format and type, or one owned by the source's package.</summary>
    private static Result CheckEntityNamespace(IRegistry registry, SilRecord record, string space, string key, string package)
    {
        SharedNamespace? shared = registry.FindSharedNamespace(space);
        if (shared != null)
        {
            if (!registry.Matches(shared.KeyPattern, key) || record.Type != shared.EntityType)
            {
                return Failure($"shared namespace '{space}' holds {shared.EntityType} entities with keys like {shared.KeyPattern}");
            }
            return Result.Success();
        }
        if (registry.OwnerOfEntityNamespace(space) != package)
        {
            return Failure($"entity namespace '{space}' is not owned by package '{package}'");
        }
        return Result.Success();
    }

    /// <summary>The id prefix of each kind.</summary>
    private static string PrefixOf(RecordKind kind)
    {
        switch (kind)
        {
            case RecordKind.Entity:
                return "ent";
            case RecordKind.Event:
                return "evt";
            case RecordKind.Relation:
                return "rel";
            default:
                return "con";
        }
    }

    /// <summary>An E-ID-NAMESPACE failure at "id".</summary>
    private static Result Failure(string message)
    {
        return Result.Failure(ErrorCodes.IdNamespace, "id", message);
    }
}
