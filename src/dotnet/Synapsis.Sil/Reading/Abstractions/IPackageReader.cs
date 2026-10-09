using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Reading.Abstractions;

/// <summary>
/// Reads a schema package (YAML, or JSON, which is valid YAML) into a <see cref="SchemaPackage"/>.
/// </summary>
public interface IPackageReader
{
    /// <summary>The package, or E-SCHEMA-PACKAGE when the text is not one.</summary>
    Result<SchemaPackage> Read(string text);
}
