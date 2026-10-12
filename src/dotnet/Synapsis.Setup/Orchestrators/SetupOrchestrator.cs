using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Synapsis.Setup;

/// <summary>Runs every setup step once, in order. A step that fails stops the run, so the process ends with an error.</summary>
public sealed class SetupOrchestrator
{
    private readonly IEnumerable<ISetupStep> _steps;
    private readonly ILogger<SetupOrchestrator> _logger;

    /// <summary>Receives all the registered steps (dependency injection keeps their registration order).</summary>
    public SetupOrchestrator(IEnumerable<ISetupStep> steps, ILogger<SetupOrchestrator> logger)
    {
        _steps = steps;
        _logger = logger;
    }

    /// <summary>Runs the steps one by one, logging each, and stops at the first that throws.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (ISetupStep step in _steps)
        {
            string name = step.GetType().Name;
            _logger.LogInformation("Setup step {Step} is running.", name);
            await step.RunAsync(cancellationToken);
            _logger.LogInformation("Setup step {Step} is done.", name);
        }
    }
}
