using System.Text.Json;

namespace Synapsis.Sil.Fingerprinting.Abstractions;

/// <summary>
/// Gives the fingerprint of a record's content: equal content gives an equal fingerprint, so comparing two short
/// texts answers "same record, or changed?". The content is everything except known_at and known_at_basis:
/// re-sending a record with another knowledge time is not a change.
/// </summary>
public interface IFingerprinter
{
    /// <summary>The fingerprint of a raw record's content, as 64 lower-case hex characters.</summary>
    string OfRecord(JsonElement record);
}
