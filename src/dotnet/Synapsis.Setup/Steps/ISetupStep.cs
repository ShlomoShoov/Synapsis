using System.Threading;
using System.Threading.Tasks;

namespace Synapsis.Setup;

/// <summary>
/// One thing the stores need before the services start, such as an index or a constraint. A step creates it only when
/// it is missing, so running it again changes nothing. SetupOrchestrator receives every step through dependency
/// injection and runs them one by one, in the order Program registers them.
/// </summary>
public interface ISetupStep
{
    /// <summary>Creates what this step is about when it is missing; throws when a store refuses or does not answer.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
