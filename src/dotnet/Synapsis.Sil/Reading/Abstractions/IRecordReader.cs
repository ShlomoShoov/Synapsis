using System.Text.Json;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Reading.Abstractions;

/// <summary>
/// Reads one raw record into the typed class of its kind (entity, event, relation, conclusion). It reads; it does
/// not judge the values.
/// </summary>
public interface IRecordReader
{
    /// <summary>The typed record, or E-KIND / E-SCHEMA when the JSON does not fit the record classes.</summary>
    Result<SilRecord> Read(JsonElement raw);
}
