using System.Collections.Generic;

namespace Synapsis.Data.Models.Records;

/// <summary>A position in a logical space, such as ["Microsoft", "Windows"] in the space "products".</summary>
public sealed class LogicalPlace
{
    /// <summary>The space's name.</summary>
    public string? Space { get; set; }

    /// <summary>The path inside it, from the top.</summary>
    public List<string> Path { get; set; } = new List<string>();
}
