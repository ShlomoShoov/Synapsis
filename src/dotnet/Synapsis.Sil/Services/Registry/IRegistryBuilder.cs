using System.Collections.Generic;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Services.Registry;

/// <summary>
/// Builds the <see cref="IRegistry"/> from schema packages (core and every source package).
/// </summary>
public interface IRegistryBuilder
{
    /// <summary>The registry, or E-SCHEMA-OWNERSHIP when two packages define the same code.</summary>
    Result<IRegistry> Build(IEnumerable<SchemaPackage> packages);
}
