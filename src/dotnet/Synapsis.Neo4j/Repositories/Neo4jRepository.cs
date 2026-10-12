using System;
using System.Threading;
using System.Threading.Tasks;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Records;

namespace Synapsis.Neo4j;

/// <summary>Defines the Neo4j hand-off without adding graph-writing rules in this phase.</summary>
public sealed class Neo4jRepository
{
    private readonly Neo4jContext _neo4jContext;

    /// <summary>Creates the repository over the shared Neo4j driver.</summary>
    public Neo4jRepository(Neo4jContext neo4jContext)
    {
        _neo4jContext = neo4jContext;
    }

    /// <summary>Accepts a typed record for the later graph implementation.</summary>
    public Task StoreAsync(SilRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _ = _neo4jContext;
        _ = cancellationToken;
        throw new NotSupportedException("Neo4j graph writes are not implemented in this phase.");
    }
}
