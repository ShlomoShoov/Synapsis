using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Http.Services.Filters;
using Synapsis.Api.Http.Services.Mappers;
using Synapsis.Api.Registration.Abstractions;
using Synapsis.Api.Registration.Models.Requests;
using Synapsis.Api.Registration.Models.Results;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Controllers;

/// <summary>PUT /schemas/{package}: registers a schema package (YAML or JSON body).</summary>
[ApiController]
public sealed class SchemasController : ControllerBase
{
    private readonly IRegistrationOrchestrator _registration;

    /// <summary>Creates the controller over the registration process.</summary>
    public SchemasController(IRegistrationOrchestrator registration)
    {
        _registration = registration;
    }

    /// <summary>201 created, 200 updated or unchanged, with {status, package, version}; or the problem.</summary>
    [HttpPut("schemas/{package}")]
    [ServiceFilter(typeof(ApiKeyFilter))]
    public async Task<IActionResult> Put(string package)
    {
        using StreamReader body = new StreamReader(Request.Body);
        string text = await body.ReadToEndAsync();
        Result<RegistrationResult> result = await _registration.RegisterAsync(new RegistrationRequest(package, text));
        if (!result.Succeeded)
        {
            return ErrorMapper.ToResult(result.Problem!);
        }
        return StatusCode(RegistrationMapper.ToStatus(result.Value!), RegistrationMapper.ToDto(result.Value!));
    }
}
