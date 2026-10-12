namespace Synapsis.Data.Models.Records;

/// <summary>A country, and maybe one of its subdivisions.</summary>
public sealed class AdminArea
{
    /// <summary>The ISO 3166-1 code, such as "US".</summary>
    public string? Country { get; set; }

    /// <summary>The ISO 3166-2 code, such as "US-CA".</summary>
    public string? Subdivision { get; set; }

    /// <summary>The place's name as the source writes it.</summary>
    public string? Name { get; set; }
}
