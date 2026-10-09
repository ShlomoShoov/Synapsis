using System.Collections.Generic;
using System.IO;
using Synapsis.Sil.Reading;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Vocabulary;

namespace Synapsis.Tests.Support;

/// <summary>
/// The registry of all the repository's real packages (core and every feeder package), built once for all tests.
/// </summary>
public static class TestRegistry
{
    /// <summary>The snapshot of every package in schemas/seed and feeders/packages.</summary>
    public static readonly RegistrySnapshot All = RegistryBuilder.Build(AllPackages());

    /// <summary>Reads core.yaml and every feeder package.</summary>
    public static List<SchemaPackage> AllPackages()
    {
        List<SchemaPackage> packages = new List<SchemaPackage>();
        packages.Add(PackageReader.Read(RepositoryFiles.ReadText("schemas/seed/core.yaml")));
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("feeders/packages"), "*.yaml"))
        {
            packages.Add(PackageReader.Read(File.ReadAllText(path)));
        }
        return packages;
    }
}
