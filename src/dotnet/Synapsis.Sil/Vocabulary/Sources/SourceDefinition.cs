namespace Synapsis.Sil.Vocabulary.Sources;

/// <summary>
/// A source of records: who publishes it, how much to trust it, and how its "known at" time is set.
/// Example: src:cisa:kev, CISA, reliability 95 (A1), published one day after an entry is added.
/// </summary>
public sealed class SourceDefinition
{
    /// <summary>The source id, "src:&lt;publisher&gt;:&lt;dataset&gt;", for example "src:cisa:kev".</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The dataset's name.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Who publishes it.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Optional link to the source.</summary>
    public string? Url { get; init; }

    /// <summary>Optional licence or terms of use.</summary>
    public string? License { get; init; }

    /// <summary>How much the source is trusted, from 0 to 100; feeders copy it onto every record.</summary>
    public int Reliability { get; init; }

    /// <summary>Why: a NATO code from A1 to F6, or measured, analyst, default or model.</summary>
    public string ReliabilityBasis { get; init; } = string.Empty;

    /// <summary>
    /// How many days after an event the source usually publishes it. Feeders use it to set "known at"; the engine
    /// waits for it before scoring a forecast.
    /// </summary>
    public double PublicationLagDays { get; init; }

    /// <summary>In plain words, how a record's "known at" time is set for this source.</summary>
    public string KnownAtRule { get; init; } = string.Empty;

    /// <summary>Optional remarks.</summary>
    public string? Notes { get; init; }
}
