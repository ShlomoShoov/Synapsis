using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Record;

/// <summary>
/// A rule about one record's values, checked on the typed record against the vocabulary (rules 3 to 12: source,
/// identity, type, valid time, knowledge time, place, measures, text, attributes, conclusion).
/// </summary>
public interface IRecordRule
{
    /// <summary>Passes, or fails with the rule's problem.</summary>
    Result Check(RecordContext context);
}
