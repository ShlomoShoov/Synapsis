using System.Collections.Generic;

namespace Synapsis.Sil.Models.Domain.Records;

/// <summary>
/// An entity: a thing that persists and that other records refer to, such as a bank, a vulnerability or a vessel.
/// Example: { "kind": "entity", "id": "ent:cve:CVE-2023-34362", "type": "Vulnerability", "name": "CVE-2023-34362", ... }
/// </summary>
public sealed class EntityRecord : SilRecord
{
    /// <summary>The entity's name as people should see it.</summary>
    public required string Name { get; init; }

    /// <summary>Other names the entity is known by, so search finds it under any of them.</summary>
    public List<string>? Aliases { get; init; }
}
