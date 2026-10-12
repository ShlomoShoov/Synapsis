using Synapsis.Data.Models.Options;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the names and defaults of the Neo4j connection settings.</summary>
public sealed class Neo4jOptionsTests
{
    /// <summary>Checks that Neo4j settings start empty and require configuration.</summary>
    [Fact]
    public void Neo4j_options_start_without_invented_connection_values()
    {
        Neo4jOptions options = new();

        Assert.Null(options.Url);
        Assert.Null(options.Username);
        Assert.Null(options.Password);
        Assert.Null(options.Database);
    }
}
