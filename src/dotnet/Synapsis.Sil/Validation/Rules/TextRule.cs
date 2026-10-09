using Synapsis.Sil.Records;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 10 (events): text must follow the event type's policy. "none": no text (measurement-only types keep the search
/// index lean). "required": a title or a body (search depends on it). For other kinds text is optional.
/// </summary>
public sealed class TextRule : IRecordRule
{
    /// <summary>Returns E-TEXT, or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord)
        {
            return null;
        }
        TextPolicy policy = context.Registry.EventTypes[eventRecord.Type].Text;
        if (policy == TextPolicy.None && eventRecord.Text != null)
        {
            return new RecordError(ErrorCodes.Text, "text", $"type '{eventRecord.Type}' carries no text");
        }
        if (policy == TextPolicy.Required && !HasWords(eventRecord))
        {
            return new RecordError(ErrorCodes.Text, "text", $"type '{eventRecord.Type}' requires text");
        }
        return null;
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
