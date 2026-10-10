namespace Synapsis.Api.Models.Registration;

/// <summary>What a registration did.</summary>
public enum RegistrationStatus
{
    /// <summary>The package is new: version 1.</summary>
    Created,

    /// <summary>The package changed: a new version.</summary>
    Updated,

    /// <summary>The package is the same as the stored version: nothing was written.</summary>
    Unchanged,
}
