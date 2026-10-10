using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Synapsis.Storage.Dal;

/// <summary>The health check behind GET /health: healthy when MongoDB answers a ping.</summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoContext _context;

    /// <summary>Creates the check over the database connection.</summary>
    public MongoHealthCheck(IMongoContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PingAsync();
            return HealthCheckResult.Healthy("MongoDB answers");
        }
        catch (Exception problem)
        {
            return HealthCheckResult.Unhealthy("MongoDB does not answer", problem);
        }
    }
}
