namespace Synapsis.Data.Models.Options;

/// <summary>Connection settings for Neo4j, from the configuration section "Neo4j".</summary>
public sealed class Neo4jOptions
{
    /// <summary>The Neo4j Bolt URI, or null when it is not configured.</summary>
    public string? Url { get; init; }

    /// <summary>The Neo4j user name, or null when it is not configured.</summary>
    public string? Username { get; init; }

    /// <summary>The Neo4j password, or null when it is not configured.</summary>
    public string? Password { get; init; }

    /// <summary>The Neo4j database name, or null when it is not configured.</summary>
    public string? Database { get; init; }
}
