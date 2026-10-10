namespace Synapsis.Api.Models.Dtos;

/// <summary>The answer to PUT /schemas/{package}.</summary>
public sealed class RegistrationDto
{
    /// <summary>"created", "updated" or "unchanged".</summary>
    public required string Status { get; init; }

    /// <summary>The package name.</summary>
    public required string Package { get; init; }

    /// <summary>The package's current version.</summary>
    public required int Version { get; init; }
}
