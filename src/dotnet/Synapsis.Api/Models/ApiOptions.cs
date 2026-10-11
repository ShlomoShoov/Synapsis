using System;

namespace Synapsis.Api.Models;

/// <summary>The HTTP settings, from the configuration section "Api" (environment variables Api__MaxBodyMb, Api__CorsOrigins__0).</summary>
public sealed class ApiOptions
{
    /// <summary>The largest request body, unpacked, in megabytes.</summary>
    public int MaxBodyMb { get; set; } = 64;

    /// <summary>The web addresses allowed to read the API from a browser (CORS), such as the UI's "http://localhost:1897".</summary>
    public string[] CorsOrigins { get; set; } = Array.Empty<string>();
}
