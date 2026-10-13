using Synapsis.Data.Dal;

namespace Synapsis.Api.Repositories;

/// <summary>
/// Neo4j for the Analyze routes: the connections of a point (direct relations, entities that take part in the same
/// events). Every query builds its conditions with GraphFilters. The queries themselves are the next task.
/// </summary>
public sealed class GraphRepository
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the repository over the Neo4j connection (each query opens a session with it).</summary>
    public GraphRepository(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }
}
