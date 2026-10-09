using System.Text.Json;
using Synapsis.Sil.Records;

namespace Synapsis.Sil.Validation.Batch;

/// <summary>
/// A record that passed the record rules (1 to 12): its position in the batch, the JSON as sent, the typed record,
/// and the fingerprint of its content.
/// </summary>
public sealed class PreparedRecord
{
    /// <summary>Creates a prepared record.</summary>
    public PreparedRecord(int index, JsonElement raw, SilRecord record, string fingerprint)
    {
        Index = index;
        Raw = raw;
        Record = record;
        Fingerprint = fingerprint;
    }

    /// <summary>The record's position in the batch, counting from 0.</summary>
    public int Index { get; }

    /// <summary>The record exactly as the producer sent it (what is stored).</summary>
    public JsonElement Raw { get; }

    /// <summary>The typed record (what the rules read).</summary>
    public SilRecord Record { get; }

    /// <summary>The fingerprint of its content (everything but known_at and its basis).</summary>
    public string Fingerprint { get; }
}
