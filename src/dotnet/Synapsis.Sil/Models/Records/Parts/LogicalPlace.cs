using System.Collections.Generic;

namespace Synapsis.Sil.Models.Records.Parts;

/// <summary>
/// A position in a registered logical space, from the top level down.
/// Example: { "space": "cyber.vendor_product", "path": ["Progress", "MOVEit Transfer"] }.
/// </summary>
public sealed class LogicalPlace
{
    /// <summary>The logical space, as registered in a schema package, for example "cyber.vendor_product".</summary>
    public required string Space { get; init; }

    /// <summary>The path from the top level down; never longer than the space's maximum depth.</summary>
    public required List<string> Path { get; init; }
}
