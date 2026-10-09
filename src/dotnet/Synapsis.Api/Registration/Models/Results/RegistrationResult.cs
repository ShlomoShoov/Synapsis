namespace Synapsis.Api.Registration.Models.Results;

/// <summary>The outcome of a registration: what it did, and the package's current version.</summary>
public sealed class RegistrationResult
{
    /// <summary>Creates a result.</summary>
    public RegistrationResult(RegistrationStatus status, string package, int version)
    {
        Status = status;
        Package = package;
        Version = version;
    }

    /// <summary>Created, updated or unchanged.</summary>
    public RegistrationStatus Status { get; }

    /// <summary>The package name.</summary>
    public string Package { get; }

    /// <summary>The package's current version.</summary>
    public int Version { get; }
}
