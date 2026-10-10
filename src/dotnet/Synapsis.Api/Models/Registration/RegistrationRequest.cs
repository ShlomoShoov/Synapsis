namespace Synapsis.Api.Models.Registration;

/// <summary>A package to register: the name it is registered under, and its text (YAML or JSON).</summary>
public sealed class RegistrationRequest
{
    /// <summary>Creates a request.</summary>
    public RegistrationRequest(string name, string text)
    {
        Name = name;
        Text = text;
    }

    /// <summary>The name it is registered under (from the request's address).</summary>
    public string Name { get; }

    /// <summary>The package as sent.</summary>
    public string Text { get; }
}
