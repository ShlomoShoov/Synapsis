using System.Collections.Generic;
using System.IO;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Tests.Support;

/// <summary>
/// The registry of all the repository's real packages (core and every feeder package), built once for all tests
/// through the components' abstractions.
/// </summary>
public static class TestRegistry
{
    /// <summary>The registry of every package in schemas/seed and feeders/packages.</summary>
    public static readonly IRegistry All = SilComponents.CreateRegistryBuilder().Build(AllPackages()).Value!;

    /// <summary>Reads core.yaml and every feeder package.</summary>
    public static List<SchemaPackage> AllPackages()
    {
        IPackageReader reader = SilComponents.CreatePackageReader();
        List<SchemaPackage> packages = new List<SchemaPackage>();
        packages.Add(reader.Read(RepositoryFiles.ReadText("schemas/seed/core.yaml")).Value!);
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("feeders/packages"), "*.yaml"))
        {
            Result<SchemaPackage> package = reader.Read(File.ReadAllText(path));
            packages.Add(package.Value!);
        }
        return packages;
    }
}
