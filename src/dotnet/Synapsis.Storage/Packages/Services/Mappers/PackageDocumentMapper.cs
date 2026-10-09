using Synapsis.Sil.Models.Domain.Vocabulary;
using Synapsis.Storage.Packages.Models.Domain;
using Synapsis.Storage.Packages.Models.Entities;

namespace Synapsis.Storage.Packages.Services.Mappers;

/// <summary>Converts between stored packages and their documents.</summary>
public static class PackageDocumentMapper
{
    /// <summary>A package as the document to store.</summary>
    public static PackageDocument ToDocument(StoredPackage package)
    {
        return new PackageDocument { Name = package.Name, Version = package.Version, Fingerprint = package.Fingerprint, Text = package.Text };
    }

    /// <summary>A document as a stored package, with the package read from its text.</summary>
    public static StoredPackage ToStoredPackage(PackageDocument document, SchemaPackage package)
    {
        return new StoredPackage
        {
            Name = document.Name,
            Version = document.Version,
            Fingerprint = document.Fingerprint,
            Text = document.Text,
            Package = package,
        };
    }
}
