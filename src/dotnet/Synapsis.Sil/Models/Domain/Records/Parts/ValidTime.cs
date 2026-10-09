namespace Synapsis.Sil.Models.Domain.Records.Parts;

/// <summary>
/// When something happened or held (valid time). The start is aligned to its precision: a "month" starts on the
/// first of the month at midnight, a "week" on a Monday. Example: { "start": "2023-06-02T00:00:00Z", "precision": "day" }.
/// </summary>
public sealed class ValidTime
{
    /// <summary>The start, as a UTC instant such as "2023-06-02T00:00:00Z".</summary>
    public required string Start { get; init; }

    /// <summary>
    /// How precisely the source knows the time: second, minute, hour, day, week, month, quarter or year
    /// (the list lives in the core package).
    /// </summary>
    public required string Precision { get; init; }

    /// <summary>Optional end, after the start; used for periods such as a quarter or a month.</summary>
    public string? End { get; init; }
}
