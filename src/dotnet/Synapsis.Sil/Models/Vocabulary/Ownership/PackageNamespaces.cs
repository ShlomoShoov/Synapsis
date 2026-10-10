using System.Collections.Generic;

namespace Synapsis.Sil.Models.Vocabulary.Ownership;

/// <summary>
/// The id namespaces a package owns. Only records whose source belongs to this package may use them, which keeps
/// ids from different feeders from colliding.
/// </summary>
public sealed class PackageNamespaces
{
    /// <summary>Namespaces for events, relations and conclusions (ids starting evt:, rel:, con:).</summary>
    public List<string> Records { get; init; } = new List<string>();

    /// <summary>Namespaces for entities (ids starting ent:).</summary>
    public List<string> Entities { get; init; } = new List<string>();
}
