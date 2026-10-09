namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// One record rule. It checks one aspect of a record that has passed rules 1 and 2 (kind and structure), and
/// returns the error it finds, or null when the record passes.
/// </summary>
public interface IRecordRule
{
    /// <summary>Checks the record; returns the first problem this rule finds, or null.</summary>
    RecordError? Check(RecordContext context);
}
