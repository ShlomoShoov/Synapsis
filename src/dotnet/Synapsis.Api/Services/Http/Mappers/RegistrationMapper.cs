using Synapsis.Api.Models.Dtos;
using Synapsis.Api.Models.Registration;
namespace Synapsis.Api.Services.Http.Mappers;

/// <summary>Turns a registration's outcome into its HTTP body and status.</summary>
public static class RegistrationMapper
{
    /// <summary>The outcome as the answer to PUT /schemas/{package}.</summary>
    public static RegistrationDto ToDto(RegistrationResult result)
    {
        return new RegistrationDto { Status = result.Status.ToString().ToLowerInvariant(), Package = result.Package, Version = result.Version };
    }

    /// <summary>201 for a new package, 200 for an updated or unchanged one.</summary>
    public static int ToStatus(RegistrationResult result)
    {
        if (result.Status == RegistrationStatus.Created)
        {
            return 201;
        }
        return 200;
    }
}
