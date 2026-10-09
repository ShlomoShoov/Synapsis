using MongoDB.Bson.Serialization.Attributes;

namespace Synapsis.Storage.Packages.Models.Entities;

/// <summary>One document of the "schema_packages" collection: the current version of one package.</summary>
public sealed class PackageDocument
{
    /// <summary>The package name.</summary>
    [BsonId]
    public string Name { get; set; } = string.Empty;

    /// <summary>The version number: 1, 2, ...</summary>
    [BsonElement("version")]
    public int Version { get; set; }

    /// <summary>The fingerprint of what it declares.</summary>
    [BsonElement("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The package as received.</summary>
    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;
}
