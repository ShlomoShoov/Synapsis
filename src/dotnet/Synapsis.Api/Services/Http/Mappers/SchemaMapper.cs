using System.Collections.Generic;
using Synapsis.Api.Models.Dtos;
using Synapsis.Sil.Services.Rules.Package;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services.Http.Mappers;

/// <summary>Turns stored schema packages into the answers of GET /schemas and GET /schemas/{package}.</summary>
public static class SchemaMapper
{
    /// <summary>The list of packages: name, version and fingerprint of each.</summary>
    public static List<SchemaSummaryDto> ToSummaries(IReadOnlyList<StoredPackage> packages)
    {
        List<SchemaSummaryDto> summaries = new List<SchemaSummaryDto>();
        foreach (StoredPackage package in packages)
        {
            summaries.Add(new SchemaSummaryDto { Name = package.Name, Version = package.Version, Fingerprint = package.Fingerprint });
        }
        return summaries;
    }

    /// <summary>One package with its content converted from its stored text (YAML or JSON) to JSON.</summary>
    public static SchemaDto ToDto(StoredPackage package)
    {
        return new SchemaDto
        {
            Name = package.Name,
            Version = package.Version,
            Fingerprint = package.Fingerprint,
            Package = YamlJson.ToJson(package.Text),
        };
    }
}
