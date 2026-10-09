namespace Synapsis.Sil.Models.Records.Parts;

/// <summary>
/// An administrative area by ISO code, one key shared by every source. Example: { "country": "US", "subdivision": "US-CA" }.
/// </summary>
public sealed class AdminArea
{
    /// <summary>The country, as an ISO 3166-1 alpha-2 code such as "US" or "SD".</summary>
    public required string Country { get; init; }

    /// <summary>Optional first-level subdivision, as an ISO 3166-2 code such as "US-CA"; it starts with the country.</summary>
    public string? Subdivision { get; init; }

    /// <summary>Optional name of the place as the source gives it, for display.</summary>
    public string? Name { get; init; }
}
