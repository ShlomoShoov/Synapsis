namespace Synapsis.Sil.Models.Records.Parts;

/// <summary>
/// Where a record can be checked at its source.
/// Example: { "url": "https://www.cisa.gov/known-exploited-vulnerabilities-catalog", "ref": "CVE-2023-34362" }.
/// </summary>
public sealed class Evidence
{
    /// <summary>A link to the source's page or file.</summary>
    public string? Url { get; init; }

    /// <summary>The record's reference inside that source.</summary>
    public string? Ref { get; init; }
}
