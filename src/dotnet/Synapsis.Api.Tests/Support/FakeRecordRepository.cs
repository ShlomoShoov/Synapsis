using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Storage.Records.Abstractions;
using Synapsis.Storage.Records.Models.Domain;
using Synapsis.Storage.Records.Models.Requests;

namespace Synapsis.Api.Tests.Support;

/// <summary>A record store in memory: the versions added, and the same answers as the real one.</summary>
public sealed class FakeRecordRepository : IRecordRepository
{
    /// <summary>Every version added, in order.</summary>
    public List<RecordVersion> Versions { get; } = new List<RecordVersion>();

    /// <summary>When true, the next write answers as if another writer stored a version first.</summary>
    public bool ConflictOnNextWrite { get; set; }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>>> StoredVersionsAsync(IReadOnlyCollection<StatementKey> keys)
    {
        HashSet<StatementKey> wanted = new HashSet<StatementKey>(keys);
        Dictionary<StatementKey, IReadOnlyList<StoredVersion>> found = new Dictionary<StatementKey, IReadOnlyList<StoredVersion>>();
        foreach (RecordVersion version in Versions)
        {
            if (!wanted.Contains(version.Key))
            {
                continue;
            }
            List<StoredVersion> list = found.TryGetValue(version.Key, out IReadOnlyList<StoredVersion>? existing) ? new List<StoredVersion>(existing) : new List<StoredVersion>();
            list.Add(new StoredVersion { Version = version.Version, Fingerprint = version.Fingerprint, KnownAt = version.KnownAt });
            found[version.Key] = list;
        }
        IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> answer = found;
        return Task.FromResult(answer);
    }

    /// <inheritdoc/>
    public Task<Result> AddAsync(IReadOnlyList<RecordVersion> versions)
    {
        if (ConflictOnNextWrite)
        {
            ConflictOnNextWrite = false;
            return Task.FromResult(Result.Failure(ErrorCodes.Unavailable, string.Empty, "another writer stored some of these first"));
        }
        Versions.AddRange(versions);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredStatement>> AsOfAsync(StatementsAsOfRequest request)
    {
        IReadOnlyList<StoredStatement> none = new List<StoredStatement>();
        return Task.FromResult(none);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, HashSet<string>>> EntityTypesAsync(IReadOnlyCollection<string> entityIds)
    {
        HashSet<string> wanted = new HashSet<string>(entityIds);
        Dictionary<string, HashSet<string>> types = new Dictionary<string, HashSet<string>>();
        foreach (RecordVersion version in Versions)
        {
            if (version.Kind == RecordKind.Entity && wanted.Contains(version.Key.Id))
            {
                if (!types.ContainsKey(version.Key.Id))
                {
                    types[version.Key.Id] = new HashSet<string>();
                }
                types[version.Key.Id].Add(version.Record.GetProperty("type").GetString()!);
            }
        }
        return Task.FromResult(types);
    }

    /// <inheritdoc/>
    public Task<HashSet<string>> ExistingAsync(IReadOnlyCollection<string> recordIds)
    {
        HashSet<string> wanted = new HashSet<string>(recordIds);
        HashSet<string> existing = new HashSet<string>();
        foreach (RecordVersion version in Versions)
        {
            if (wanted.Contains(version.Key.Id))
            {
                existing.Add(version.Key.Id);
            }
        }
        return Task.FromResult(existing);
    }
}
