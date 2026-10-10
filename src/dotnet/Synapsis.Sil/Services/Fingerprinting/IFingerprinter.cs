using System.Text.Json;
using Synapsis.Sil.Models.Vocabulary;
namespace Synapsis.Sil.Services.Fingerprinting;

/// <summary>
/// Gives the fingerprint of a record's or a package's content: equal content gives an equal fingerprint, so comparing
/// two short texts answers "same, or changed?". A record's content is everything except known_at and known_at_basis:
/// re-sending a record with another knowledge time is not a change. A package's content is what it declares, so
/// spacing, comments and key order in its YAML do not count.
/// </summary>
public interface IFingerprinter
{
    /// <summary>The fingerprint of a raw record's content, as 64 lower-case hex characters.</summary>
    string OfRecord(JsonElement record);

    /// <summary>The fingerprint of a package's content, as 64 lower-case hex characters.</summary>
    string OfPackage(SchemaPackage package);
}
