using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Models.Requests;

namespace Synapsis.Sil.Validation.Abstractions;

/// <summary>
/// Validates one raw record on its own (rules 1 to 12): its envelope, then its values against the vocabulary.
/// </summary>
public interface IRecordValidator
{
    /// <summary>The typed record when every rule passes; otherwise the problem of the first rule that fails.</summary>
    Result<SilRecord> Validate(RecordValidationRequest request);
}
