using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Synapsis.Api.Models.Configuration;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Services.Http;

/// <summary>
/// Guards the writes: when a key is configured, the request must carry it as "Authorization: Bearer &lt;key&gt;" or
/// "X-Api-Key: &lt;key&gt;", or it is refused with E-AUTH (401). The comparison takes the same time whatever the key,
/// so its length and content cannot be guessed from timing.
/// </summary>
public sealed class ApiKeyFilter : IAuthorizationFilter
{
    private readonly ApiOptions _options;

    /// <summary>Creates the filter over the configured key.</summary>
    public ApiKeyFilter(ApiOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (_options.Key.Length == 0)
        {
            return;
        }
        string? given = KeyOf(context.HttpContext.Request);
        if (given != null && SameKey(given, _options.Key))
        {
            return;
        }
        Problem problem = new Problem(ErrorCodes.Auth, string.Empty, "a valid API key is required (Authorization: Bearer <key>)");
        context.Result = ErrorMapper.ToResult(problem);
    }

    /// <summary>The key the request carries, or null.</summary>
    private static string? KeyOf(HttpRequest request)
    {
        string apiKey = request.Headers["X-Api-Key"].ToString();
        if (apiKey.Length > 0)
        {
            return apiKey;
        }
        string authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization.Substring("Bearer ".Length);
        }
        return null;
    }

    /// <summary>True when the two keys are equal, compared in constant time.</summary>
    private static bool SameKey(string given, string expected)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
    }
}
