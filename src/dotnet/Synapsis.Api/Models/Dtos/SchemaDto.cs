using System.Text.Json.Nodes;

namespace Synapsis.Api.Models.Dtos;

/// <summary>GET /schemas/{package}: the current version of one schema package, its content as JSON.</summary>
public sealed class SchemaDto
{
    /// <summary>The package name, such as "kev".</summary>
    public required string Name { get; init; }

    /// <summary>Its current version.</summary>
    public required int Version { get; init; }

    /// <summary>The fingerprint of its text.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>The package itself (sources, types, roles, measures, logical spaces, namespaces), converted from YAML to JSON.</summary>
    public required JsonNode? Package { get; init; }
}
