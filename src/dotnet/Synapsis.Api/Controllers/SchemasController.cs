using Synapsis.Api.Services.Swagger;
using System.Collections.Generic;
using Synapsis.Storage.Models;
using Synapsis.Storage.Repositories;
using Synapsis.Sil.Models.Results;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Api.Orchestrators;
using Synapsis.Api.Models.Registration;

namespace Synapsis.Api.Controllers;

/// <summary>The schema packages: GET /schemas and GET /schemas/{package} read them; PUT /schemas/{package} registers one (YAML or JSON body).</summary>
[ApiController]
public sealed class SchemasController : ControllerBase
{
    private readonly IRegistrationOrchestrator _registration;
    private readonly IPackageRepository _packages;

    /// <summary>Creates the controller over the registration process and the package store (reads need no process).</summary>
    public SchemasController(IRegistrationOrchestrator registration, IPackageRepository packages)
    {
        _registration = registration;
        _packages = packages;
    }

    /// <summary>200 with every current package: name, version, fingerprint.</summary>
    [HttpGet("schemas")]
    public async Task<IActionResult> List()
    {
        IReadOnlyList<StoredPackage> packages = await _packages.AllAsync();
        return Ok(SchemaMapper.ToSummaries(packages));
    }

    /// <summary>200 with one package's current version and its content as JSON; 404 E-NOT-FOUND when there is no such package.</summary>
    [HttpGet("schemas/{package}")]
    public async Task<IActionResult> Get(string package)
    {
        IReadOnlyList<StoredPackage> packages = await _packages.AllAsync();
        foreach (StoredPackage stored in packages)
        {
            if (stored.Name == package)
            {
                return Ok(SchemaMapper.ToDto(stored));
            }
        }
        return ErrorMapper.ToResult(new Problem(ErrorCodes.NotFound, string.Empty, $"no schema package '{package}'"));
    }

    /// <summary>201 created, 200 updated or unchanged, with {status, package, version}; or the problem.</summary>
    [HttpPut("schemas/{package}")]
    [RawBody("application/yaml")]
    public async Task<IActionResult> Put(string package)
    {
        using StreamReader body = new StreamReader(Request.Body);
        string text = await body.ReadToEndAsync();
        Result<RegistrationResult> result = await _registration.RegisterAsync(package, text);
        if (!result.Succeeded)
        {
            return ErrorMapper.ToResult(result.Problem!);
        }
        return StatusCode(RegistrationMapper.ToStatus(result.Value!), RegistrationMapper.ToDto(result.Value!));
    }
}
