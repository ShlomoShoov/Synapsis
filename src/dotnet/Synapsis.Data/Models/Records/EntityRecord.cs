using System.Collections.Generic;

namespace Synapsis.Data.Models.Records;

/// <summary>An entity: a thing that exists, such as a bank, a vulnerability or a country.</summary>
public sealed class EntityRecord : SilRecord
{
    /// <summary>Its name.</summary>
    public string? Name { get; set; }

    /// <summary>Other names it is known by.</summary>
    public List<string> Aliases { get; set; } = new List<string>();
}
