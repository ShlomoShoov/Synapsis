namespace Synapsis.Sil.Models.Versioning;

/// <summary>
/// What identifies a statement: a record id as stated by one source. The same id from two sources is two statements,
/// never a conflict; each has its own versions. Two keys with the same id and source are equal.
/// </summary>
public sealed record StatementKey
{
    /// <summary>The record id, for example "evt:kev:CVE-2023-34362".</summary>
    public required string Id { get; init; }

    /// <summary>The source, for example "src:cisa:kev".</summary>
    public required string Source { get; init; }
}
