namespace Synapsis.Api.Models.Dtos;

/// <summary>One schema package in the list of GET /schemas: { "name": "kev", "version": 3, "fingerprint": "…" }.</summary>
public sealed class SchemaSummaryDto
{
    /// <summary>The package name, such as "kev".</summary>
    public required string Name { get; init; }

    /// <summary>Its current version (1, 2, …).</summary>
    public required int Version { get; init; }

    /// <summary>The fingerprint of its text: it changes whenever the package changes.</summary>
    public required string Fingerprint { get; init; }
}
