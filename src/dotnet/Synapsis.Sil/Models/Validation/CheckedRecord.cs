using System.Text.Json;
using Synapsis.Sil.Models.Records;
namespace Synapsis.Sil.Models.Validation;

/// <summary>
/// A record that passed the rules about itself (1 to 12): its position in the batch, the JSON as sent, the typed
/// record, and the fingerprint of its content.
/// </summary>
public sealed class CheckedRecord
{
    /// <summary>Creates a checked record.</summary>
    public CheckedRecord(int index, JsonElement raw, SilRecord record, string fingerprint)
    {
        Index = index;
        Raw = raw;
        Record = record;
        Fingerprint = fingerprint;
    }

    /// <summary>The record's position in the batch, counting from 0.</summary>
    public int Index { get; }

    /// <summary>The record exactly as the producer sent it.</summary>
    public JsonElement Raw { get; }

    /// <summary>The typed record.</summary>
    public SilRecord Record { get; }

    /// <summary>The fingerprint of its content (everything but known_at and its basis).</summary>
    public string Fingerprint { get; }
}
