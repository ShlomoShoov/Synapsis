using System.Collections.Generic;

namespace Synapsis.Sil.Validation.Services.BatchRules;

/// <summary>
/// The records a batch may point to, for <see cref="ReferenceRule"/>: those already stored (asked once per batch) and
/// those accepted earlier in the same batch (added one by one, in dependency order).
/// </summary>
public sealed class KnownRecords
{
    private readonly Dictionary<string, HashSet<string>> _storedEntityTypes;
    private readonly HashSet<string> _storedIds;
    private readonly Dictionary<string, HashSet<string>> _batchEntityTypes = new Dictionary<string, HashSet<string>>();
    private readonly HashSet<string> _batchIds = new HashSet<string>();

    /// <summary>Starts with what the store holds.</summary>
    public KnownRecords(Dictionary<string, HashSet<string>> storedEntityTypes, HashSet<string> storedIds)
    {
        _storedEntityTypes = storedEntityTypes;
        _storedIds = storedIds;
    }

    /// <summary>True when the record is stored or accepted earlier in the batch.</summary>
    public bool Exists(string recordId)
    {
        return _batchIds.Contains(recordId) || _storedIds.Contains(recordId);
    }

    /// <summary>The types an entity has, from the batch and the store together; empty when it exists nowhere.</summary>
    public HashSet<string> TypesOf(string entityId)
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

    /// <summary>Adds a record accepted in this batch; an entity also adds its type.</summary>
    public void Add(string recordId, string? entityType)
    {
        _batchIds.Add(recordId);
        if (entityType == null)
        {
            return;
        }
        if (!_batchEntityTypes.TryGetValue(recordId, out HashSet<string>? types))
        {
            types = new HashSet<string>();
            _batchEntityTypes[recordId] = types;
        }
        types.Add(entityType);
    }
}
