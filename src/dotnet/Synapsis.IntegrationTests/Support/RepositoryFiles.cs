using System;
using System.IO;

namespace Synapsis.IntegrationTests.Support;

/// <summary>Finds files of the repository (conformance cases, schema packages), so the tests use the real contract files.</summary>
public static class RepositoryFiles
{
    /// <summary>The repository's root folder: the first folder above the test binaries that holds schemas/ and feeders/.</summary>
    public static readonly string Root = FindRoot();

    /// <summary>The text of a file given relative to the repository root.</summary>
    public static string ReadText(string relativePath)
    {
        return File.ReadAllText(Path.Combine(Root, relativePath));
    }

    /// <summary>Walks up from the test binaries until it finds the repository root.</summary>
    private static string FindRoot()
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, "schemas")) && Directory.Exists(Path.Combine(folder.FullName, "feeders")))
            {
                return folder.FullName;
            }
            folder = folder.Parent;
        }
        throw new InvalidOperationException("The repository root (with schemas/ and feeders/) was not found.");
    }
}
