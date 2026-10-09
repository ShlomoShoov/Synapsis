using Synapsis.Sil.Vocabulary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Synapsis.Sil.Reading;

/// <summary>
/// Turns a schema package, written in YAML or JSON (JSON is valid YAML), into a <see cref="SchemaPackage"/>.
/// YAML names are snake_case (known_at_rule, max_depth); a name the C# classes do not know is an error.
/// </summary>
public static class PackageReader
{
    /// <summary>The YAML reader, built once: snake_case names, snake_case enum values (string_list).</summary>
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>Reads one package from its text.</summary>
    /// <exception cref="YamlDotNet.Core.YamlException">The text is not a package (bad YAML, an unknown field).</exception>
    public static SchemaPackage Read(string text)
    {
        return Deserializer.Deserialize<SchemaPackage>(text);
    }
}
