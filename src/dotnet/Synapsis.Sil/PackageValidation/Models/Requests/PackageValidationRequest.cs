using System.Collections.Generic;
using Synapsis.Sil.Models.Domain.Vocabulary;

namespace Synapsis.Sil.PackageValidation.Models.Requests;

/// <summary>What validating a package needs: the package as sent and as read, its stored version, and the other packages.</summary>
public sealed class PackageValidationRequest
{
    /// <summary>Creates a request.</summary>
    public PackageValidationRequest(string text, string expectedName, SchemaPackage package, SchemaPackage? stored, IReadOnlyList<SchemaPackage> others)
    {
        Text = text;
        ExpectedName = expectedName;
        Package = package;
        Stored = stored;
        Others = others;
    }

    /// <summary>The package as sent (YAML or JSON).</summary>
    public string Text { get; }

    /// <summary>The name the package must have (the name in the request's address).</summary>
    public string ExpectedName { get; }

    /// <summary>The package, read.</summary>
    public SchemaPackage Package { get; }

    /// <summary>The version of this package stored now; null when it is new.</summary>
    public SchemaPackage? Stored { get; }

    /// <summary>Every other current package.</summary>
    public IReadOnlyList<SchemaPackage> Others { get; }
}
