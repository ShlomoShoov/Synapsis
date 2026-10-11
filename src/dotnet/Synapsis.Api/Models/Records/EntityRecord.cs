namespace Synapsis.Api.Models.Records;

/// <summary>An entity: a thing that exists, such as a bank, a vulnerability or a country.</summary>
public sealed class EntityRecord : SilRecord
{
    /// <summary>Its name (required).</summary>
    public string? Name { get; set; }
}
