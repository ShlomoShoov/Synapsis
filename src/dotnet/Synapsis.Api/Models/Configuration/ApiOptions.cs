using System;

namespace Synapsis.Api.Models.Configuration;

/// <summary>The HTTP settings, from the configuration section "Api" (environment variables Api__MaxBodyMb).</summary>
public sealed class ApiOptions
{
    /// <summary>The largest request body, unpacked, in megabytes.</summary>
    public int MaxBodyMb { get; set; } = 64;

    /// <summary>
    /// The web origins allowed to call the API from a browser (CORS), such as "http://localhost:1897" for the UI.
    /// Empty: no browser origin is allowed.
    /// </summary>
    public string[] CorsOrigins { get; set; } = Array.Empty<string>();
}
