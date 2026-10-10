using System;

namespace Synapsis.Sil.Models.Versioning;

/// <summary>What the planner needs to know about one stored version of a statement.</summary>
public sealed class StoredVersion
{
    /// <summary>The version number: 1, 2, ...</summary>
    public required int Version { get; init; }

    /// <summary>The fingerprint of its content.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>When it became known (UTC).</summary>
    public required DateTime KnownAt { get; init; }
}
