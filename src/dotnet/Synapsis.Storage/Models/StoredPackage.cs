using Synapsis.Sil.Models.Vocabulary;
namespace Synapsis.Storage.Models;

/// <summary>The current version of a schema package: its text as received, and the package read from it.</summary>
public sealed class StoredPackage
{
    /// <summary>The package name, for example "kev".</summary>
    public required string Name { get; init; }

    /// <summary>The version number: 1, 2, ...</summary>
    public required int Version { get; init; }

    /// <summary>The fingerprint of what it declares (comments and spacing do not count).</summary>
    public required string Fingerprint { get; init; }

    /// <summary>The package as received (YAML or JSON).</summary>
    public required string Text { get; init; }

    /// <summary>The package, read.</summary>
    public required SchemaPackage Package { get; init; }
}
