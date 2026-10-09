using System.Collections.Generic;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;

namespace Synapsis.Sil.Registry.Abstractions;

/// <summary>
/// Builds the <see cref="IRegistry"/> from schema packages (core and every source package).
/// </summary>
public interface IRegistryBuilder
{
    /// <summary>The registry, or E-SCHEMA-OWNERSHIP when two packages define the same code.</summary>
    Result<IRegistry> Build(IEnumerable<SchemaPackage> packages);
}
