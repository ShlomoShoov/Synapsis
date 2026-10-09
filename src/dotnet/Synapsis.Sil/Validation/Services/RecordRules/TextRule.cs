using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Domain.Vocabulary.Types;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models.Requests;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 10 (events): text must follow the event type's policy. "none": no text (measurement-only types keep the search
/// index lean). "required": a title or a body (search depends on it). For other kinds text is optional.
/// </summary>
public sealed class TextRule : IRecordRule
{
    /// <summary>Passes, or fails with E-TEXT.</summary>
    public Result Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord)
        {
            return Result.Success();
        }
        TextPolicy policy = context.Registry.FindEventType(eventRecord.Type)!.Text;
        if (policy == TextPolicy.None && eventRecord.Text != null)
        {
            return Result.Failure(ErrorCodes.Text, "text", $"type '{eventRecord.Type}' carries no text");
        }
        if (policy == TextPolicy.Required && !HasWords(eventRecord))
        {
            return Result.Failure(ErrorCodes.Text, "text", $"type '{eventRecord.Type}' requires text");
        }
        return Result.Success();
    }

    /// <summary>True when the record has a non-empty title or body.</summary>
    private static bool HasWords(EventRecord eventRecord)
    {
        if (eventRecord.Text == null)
        {
            return false;
        }
        return !string.IsNullOrEmpty(eventRecord.Text.Title) || !string.IsNullOrEmpty(eventRecord.Text.Body);
    }
}
