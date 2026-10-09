using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Synapsis.Sil.Services.Descriptions;

/// <summary>
/// Reads the description files built into this library at compile time from the repository's one copy in schemas/
/// (see Synapsis.Sil.csproj): the record language's JSON Schema, the package format, and the core package. Nothing is
/// looked up on disk at run time.
/// </summary>
public static class DescriptionFiles
{
    /// <summary>The record language (bundle and records): schemas/sil/sil-1.2.schema.json.</summary>
    public const string RecordSchema = "sil-1.2.schema.json";

    /// <summary>The schema-package format: schemas/sil/schema-package.schema.json.</summary>
    public const string PackageSchema = "schema-package.schema.json";

    /// <summary>The system's own vocabulary, seeded by the API: schemas/seed/core.yaml.</summary>
    public const string CorePackage = "core.yaml";

    /// <summary>The text of a built-in file.</summary>
    public static string ReadText(string name)
    {
        using Stream stream = Open(name);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>A built-in JSON file, parsed.</summary>
    public static JsonNode ReadJson(string name)
    {
        using Stream stream = Open(name);
        JsonNode? json = JsonNode.Parse(stream);
        if (json == null)
        {
            throw new InvalidOperationException($"The built-in file '{name}' is empty.");
        }
        return json;
    }

    /// <summary>Opens a built-in file; a missing one is a broken build.</summary>
    private static Stream Open(string name)
    {
        Assembly library = typeof(DescriptionFiles).Assembly;
        Stream? stream = library.GetManifestResourceStream(name);
        if (stream == null)
        {
            throw new InvalidOperationException($"The built-in file '{name}' is missing from Synapsis.Sil.");
        }
        return stream;
    }
}
