using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Synapsis.Sil.Reading.Services.Readers;

/// <summary>
/// Reads a schema package with YamlDotNet: snake_case names (known_at_rule, max_depth) and snake_case enum values
/// (string_list); a name the C# classes do not know is an error.
/// </summary>
public sealed class PackageReader : IPackageReader
{
    /// <summary>The YAML reader, built once.</summary>
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>The package, or E-SCHEMA-PACKAGE when the text is not YAML or does not fit the package classes.</summary>
    public Result<SchemaPackage> Read(string text)
    {
        try
        {
            SchemaPackage? package = Deserializer.Deserialize<SchemaPackage>(text);
            if (package == null)
            {
                return Result<SchemaPackage>.Failure(ErrorCodes.SchemaPackage, string.Empty, "the text holds no package");
            }
            return Result<SchemaPackage>.Success(package);
        }
        catch (YamlException problem)
        {
            return Result<SchemaPackage>.Failure(ErrorCodes.SchemaPackage, string.Empty, problem.Message);
        }
    }
}
