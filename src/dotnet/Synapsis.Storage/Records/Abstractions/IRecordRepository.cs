using System.Collections.Generic;
using System.Threading.Tasks;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Storage.Records.Models.Domain;
using Synapsis.Storage.Records.Models.Requests;

namespace Synapsis.Storage.Records.Abstractions;

/// <summary>
/// Keeps every version of every statement (a record id as stated by one source) and answers questions about them.
/// Versions are only added, never changed or removed. It also answers validation's questions about stored records
/// (<see cref="IReferenceResolver"/>).
/// </summary>
public interface IRecordRepository : IReferenceResolver
{
    /// <summary>The stored versions of each of these statements; a statement with none is missing from the answer.</summary>
    Task<IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>>> StoredVersionsAsync(IReadOnlyCollection<StatementKey> keys);

    /// <summary>
    /// Adds these versions in one write. E-UNAVAILABLE when another writer stored one of them first (the same statement
    /// and version number): the producer sends the batch again, and the versions now stored are found unchanged.
    /// </summary>
    Task<Result> AddAsync(IReadOnlyList<RecordVersion> versions);

    /// <summary>For each source that stated the id (or only the one asked), its latest version known by the date asked; empty when none.</summary>
    Task<IReadOnlyList<StoredStatement>> AsOfAsync(StatementsAsOfRequest request);
}
