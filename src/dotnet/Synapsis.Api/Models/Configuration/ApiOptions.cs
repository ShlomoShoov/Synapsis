namespace Synapsis.Api.Models.Configuration;

/// <summary>The HTTP settings, from the configuration section "Api" (environment variables Api__Key, Api__MaxBodyMb).</summary>
public sealed class ApiOptions
{
    /// <summary>The key writes must carry (PUT /schemas, POST /ingest); empty means no key is required.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The largest request body, unpacked, in megabytes.</summary>
    public int MaxBodyMb { get; set; } = 64;
}
