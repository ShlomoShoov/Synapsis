using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Synapsis.Data.Dal;

/// <summary>The check behind GET /health: healthy when MongoDB answers a ping.</summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly MongoContext _mongoContext;

    /// <summary>Creates the check over the database connection.</summary>
    public MongoHealthCheck(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    /// <summary>Healthy when MongoDB answers, unhealthy otherwise.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _mongoContext.PingAsync();
            return HealthCheckResult.Healthy("MongoDB answers");
        }
        catch (Exception problem)
        {
            return HealthCheckResult.Unhealthy("MongoDB does not answer", problem);
        }
    }
}
