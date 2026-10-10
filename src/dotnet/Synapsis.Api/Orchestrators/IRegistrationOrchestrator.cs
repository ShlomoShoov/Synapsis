using System.Threading.Tasks;
using Synapsis.Api.Models.Registration;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Orchestrators;

/// <summary>
/// Registers one schema package: reads it, compares it with the stored version, validates it, keeps it as the next
/// version and puts the new vocabulary in force. Registrations run one at a time.
/// </summary>
public interface IRegistrationOrchestrator
{
    /// <summary>
    /// Created, updated or unchanged, with the version; or the first problem: E-SCHEMA-PACKAGE, E-SCHEMA-BREAKING,
    /// E-SCHEMA-REF, E-SCHEMA-OWNERSHIP, E-SCHEMA-VERSION. A refused package leaves the vocabulary as it was.
    /// </summary>
    Task<Result<RegistrationResult>> RegisterAsync(RegistrationRequest request);
}
