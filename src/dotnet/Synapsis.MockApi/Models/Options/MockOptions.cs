namespace Synapsis.MockApi.Models.Options;

/// <summary>
/// The mock's settings, from the configuration section "Mock" (launchSettings.json for dotnet run, the environment in
/// Compose, such as Mock__CorsOrigins__0). The mock has no appsettings.json, so it can be tested beside the real API.
/// </summary>
public sealed class MockOptions
{
    /// <summary>The browser addresses allowed to read the mock (the UI: http://localhost:1897 and http://localhost:5173).</summary>
    public string[] CorsOrigins { get; set; } = new string[0];
}
