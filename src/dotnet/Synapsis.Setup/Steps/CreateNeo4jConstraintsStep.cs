using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using Synapsis.Data.Dal;

namespace Synapsis.Setup;

/// <summary>
/// Creates the Neo4j constraints and index the projector relies on: one node per id for entities, events and
/// conclusions (which also makes finding a node by id fast), and an index on the events' known_at.
/// </summary>
public sealed class CreateNeo4jConstraintsStep : ISetupStep
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the step over the Neo4j connection.</summary>
    public CreateNeo4jConstraintsStep(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }

    /// <summary>Runs each statement; "IF NOT EXISTS" makes a second run do nothing.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string[] statements =
        {
            "CREATE CONSTRAINT entity_id IF NOT EXISTS FOR (n:Entity) REQUIRE n.id IS UNIQUE",
            "CREATE CONSTRAINT event_id IF NOT EXISTS FOR (n:Event) REQUIRE n.id IS UNIQUE",
            "CREATE CONSTRAINT conclusion_id IF NOT EXISTS FOR (n:Conclusion) REQUIRE n.id IS UNIQUE",
            "CREATE INDEX event_known_at IF NOT EXISTS FOR (n:Event) ON (n.known_at)"
        };
        await using IAsyncSession session = _neo4jContext.OpenSession();
        foreach (string statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IResultCursor result = await session.RunAsync(statement);
            await result.ConsumeAsync();
        }
    }
}
