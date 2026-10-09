using System.Collections.Generic;
using System.IO;
using Synapsis.Sil;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Api.Tests.Support;

/// <summary>The repository's real schema packages and the vocabulary they make.</summary>
public static class TestPackages
{
    /// <summary>The text of a package: "core" is schemas/seed/core.yaml, any other name is feeders/packages/{name}.yaml.</summary>
    public static string Text(string name)
    {
        if (name == "core")
        {
            return RepositoryFiles.ReadText("schemas/seed/core.yaml");
        }
        return RepositoryFiles.ReadText($"feeders/packages/{name}.yaml");
    }

    /// <summary>The vocabulary of every real package.</summary>
    public static IRegistry Vocabulary()
    {
        List<SchemaPackage> packages = new List<SchemaPackage>();
        packages.Add(SilComponents.CreatePackageReader().Read(Text("core")).Value!);
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("feeders/packages"), "*.yaml"))
        {
            packages.Add(SilComponents.CreatePackageReader().Read(File.ReadAllText(path)).Value!);
        }
        return SilComponents.CreateRegistryBuilder().Build(packages).Value!;
    }
}
