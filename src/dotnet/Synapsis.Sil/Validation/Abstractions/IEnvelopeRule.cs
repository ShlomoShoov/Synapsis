using System.Text.Json;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Validation.Abstractions;

/// <summary>
/// A rule about a record's envelope, checked on the raw JSON before the record is read (rules 1 and 2: the kind,
/// the structure).
/// </summary>
public interface IEnvelopeRule
{
    /// <summary>Passes, or fails with the rule's problem.</summary>
    Result Check(JsonElement raw);
}
