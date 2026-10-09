using System.Collections.Generic;
using System.IO;
using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Xunit;

namespace Synapsis.Sil.Tests.Support;

/// <summary>The repository's real schema packages, by name, as text and read; and requests built from them.</summary>
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

    /// <summary>A package read from its text; the test fails when it cannot be read.</summary>
    public static SchemaPackage Read(string text)
    {
        Result<SchemaPackage> result = SilComponents.CreatePackageReader().Read(text);
        Assert.True(result.Succeeded, result.Problem?.Message);
        return result.Value!;
    }

    /// <summary>
    /// Every package name in the order they can be registered: core, then the shared health and social, then the
    /// feeders' own packages alphabetically.
    /// </summary>
    public static List<string> NamesInRegistrationOrder()
    {
        List<string> names = new List<string> { "core", "health", "social" };
        List<string> feeders = new List<string>();
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("feeders/packages"), "*.yaml"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!names.Contains(name))
            {
                feeders.Add(name);
            }
        }
        feeders.Sort(string.CompareOrdinal);
        names.AddRange(feeders);
        return names;
    }

    /// <summary>Every package except the named one, read.</summary>
    public static List<SchemaPackage> AllExcept(string name)
    {
        List<SchemaPackage> others = new List<SchemaPackage>();
        foreach (string other in NamesInRegistrationOrder())
        {
            if (other != name)
            {
                others.Add(Read(Text(other)));
            }
        }
        return others;
    }

    /// <summary>A request to register this text under this name, with no stored version, against every other package.</summary>
    public static PackageValidationRequest NewRequest(string name, string text)
    {
        return new PackageValidationRequest(text, name, Read(text), null, AllExcept(name));
    }

    /// <summary>A request to register this text as a new version of the named real package.</summary>
    public static PackageValidationRequest UpdateRequest(string name, string text)
    {
        return new PackageValidationRequest(text, name, Read(text), Read(Text(name)), AllExcept(name));
    }
}
