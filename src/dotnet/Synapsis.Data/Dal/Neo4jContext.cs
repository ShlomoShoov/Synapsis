using System;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>Owns the reusable Neo4j driver connection for a process.</summary>
public sealed class Neo4jContext : IAsyncDisposable
{
    private readonly IDriver _driver;

    /// <summary>Creates a Neo4j driver from the required configuration.</summary>
    public Neo4jContext(Neo4jOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Password) || string.IsNullOrWhiteSpace(options.Database))
        {
            throw new InvalidOperationException("Neo4j Url, Username, Password and Database are required.");
        }

        Database = options.Database;
        _driver = GraphDatabase.Driver(options.Url, AuthTokens.Basic(options.Username, options.Password));
    }

    /// <summary>The configured Neo4j database name.</summary>
    public string Database { get; }

    /// <summary>Opens a session for the configured Neo4j database.</summary>
    public IAsyncSession OpenSession()
    {
        return _driver.AsyncSession(configuration => configuration.WithDatabase(Database));
    }

    /// <summary>Closes the Neo4j driver asynchronously.</summary>
    public ValueTask DisposeAsync()
    {
        return _driver.DisposeAsync();
    }
}
