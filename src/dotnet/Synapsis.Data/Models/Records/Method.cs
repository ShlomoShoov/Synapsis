namespace Synapsis.Data.Models.Records;

/// <summary>Who or what made a conclusion.</summary>
public sealed class Method
{
    /// <summary>human, agent or system.</summary>
    public string? By { get; set; }

    /// <summary>The method's name.</summary>
    public string? Name { get; set; }

    /// <summary>The method's version.</summary>
    public string? Version { get; set; }
}
