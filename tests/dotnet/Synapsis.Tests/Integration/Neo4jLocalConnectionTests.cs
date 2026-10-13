using System;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;
using Xunit;

namespace Synapsis.Tests.Integration;

/// <summary>Checks the local development connection when Neo4j authentication is disabled.</summary>
public sealed class Neo4jLocalConnectionTests
{
    /// <summary>A connection without credentials can query an auth-free Neo4j instance.</summary>
    [Fact]
    public async Task Local_neo4j_connection_works_without_a_password()
    {
        string? url = Environment.GetEnvironmentVariable("SYNAPSIS_NEO4J_TEST_AUTHLESS_URL");
        if (string.IsNullOrWhiteSpace(url)) return;
        Neo4jOptions options = new Neo4jOptions { Url = url, Database = "neo4j" };
        await using Neo4jContext context = new Neo4jContext(options);
        await using IAsyncSession session = context.OpenSession();

        IResultCursor cursor = await session.RunAsync("RETURN 1 AS ready");
        IRecord result = await cursor.SingleAsync();
        Assert.Equal(1, result["ready"].As<long>());
    }
}
