using System;
using System.IO;

namespace Synapsis.Api.Tests.Support;

/// <summary>
/// Finds files of the repository (conformance cases, schema packages), so the tests read the real contract files
/// instead of copies.
/// </summary>
public static class RepositoryFiles
{
    /// <summary>The repository's root folder: the first folder above the test binaries that holds schemas/ and feeders/.</summary>
    public static readonly string Root = FindRoot();

    /// <summary>The full path of a file or folder given relative to the repository root.</summary>
    public static string PathOf(string relativePath)
    {
        return Path.Combine(Root, relativePath);
    }

    /// <summary>The text of a file given relative to the repository root.</summary>
    public static string ReadText(string relativePath)
    {
        return File.ReadAllText(PathOf(relativePath));
    }

    /// <summary>Walks up from the test binaries until it finds the repository root.</summary>
    private static string FindRoot()
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            bool hasSchemas = Directory.Exists(Path.Combine(folder.FullName, "schemas"));
            bool hasFeeders = Directory.Exists(Path.Combine(folder.FullName, "feeders"));
            if (hasSchemas && hasFeeders)
            {
                return folder.FullName;
            }
            folder = folder.Parent;
        }
        throw new InvalidOperationException("The repository root (with schemas/ and feeders/) was not found.");
    }
}
