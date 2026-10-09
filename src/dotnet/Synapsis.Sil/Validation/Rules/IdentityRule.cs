using Synapsis.Sil.Records;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Vocabulary.Ownership;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 4: an id is "prefix:namespace:key". The prefix must match the kind (ent, evt, rel, con), and the namespace
/// must be owned by the source's package, or, for an entity, be a shared namespace (such as cve) whose key format
/// and entity type it respects. This keeps ids from different feeders from colliding.
/// </summary>
public sealed class IdentityRule : IRecordRule
{
    /// <summary>Returns E-ID-NAMESPACE, or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        SilRecord record = context.Record;
        string[] parts = record.Id.Split(':', 3);
        string prefix = parts[0];
        string space = parts[1];
        string key = parts[2];
        string expectedPrefix = PrefixOf(record.Kind);
        if (prefix != expectedPrefix)
        {
            return Error($"a {KindName(record.Kind)} id starts with '{expectedPrefix}:'");
        }
        string package = context.Registry.SourcePackages[record.Source];
        if (record.Kind == RecordKind.Entity)
        {
            return CheckEntityNamespace(context.Registry, record, space, key, package);
        }
        if (!context.Registry.RecordNamespaces.TryGetValue(space, out string? owner) || owner != package)
        {
            return Error($"namespace '{space}' is not owned by package '{package}' (source {record.Source})");
        }
        return null;
    }

    /// <summary>An entity id: a shared namespace with its key format and type, or one owned by the source's package.</summary>
    private static RecordError? CheckEntityNamespace(RegistrySnapshot registry, SilRecord record, string space,
        string key, string package)
    {
        if (registry.SharedNamespaces.TryGetValue(space, out SharedNamespace? shared))
        {
            bool keyFits = registry.Patterns[shared.KeyPattern].IsMatch(key);
            if (!keyFits || record.Type != shared.EntityType)
            {
                return Error($"shared namespace '{space}' holds {shared.EntityType} entities with keys like {shared.KeyPattern}");
            }
            return null;
        }
        if (!registry.EntityNamespaces.TryGetValue(space, out string? owner) || owner != package)
        {
            return Error($"entity namespace '{space}' is not owned by package '{package}'");
        }
        return null;
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

    /// <summary>The kind in lower case, for messages.</summary>
    private static string KindName(RecordKind kind)
    {
        return kind.ToString().ToLowerInvariant();
    }

    /// <summary>An E-ID-NAMESPACE error at "id".</summary>
    private static RecordError Error(string message)
    {
        return new RecordError(ErrorCodes.IdNamespace, "id", message);
    }
}
