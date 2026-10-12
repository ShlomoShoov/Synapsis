namespace Synapsis.Data.Models.Records;

/// <summary>Where a record can be checked.</summary>
public sealed class Evidence
{
    /// <summary>A web address.</summary>
    public string? Url { get; set; }

    /// <summary>Another reference, such as a document number.</summary>
    public string? Ref { get; set; }
}
