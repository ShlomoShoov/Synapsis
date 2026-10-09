using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.PackageValidation.Abstractions;
using Synapsis.Sil.PackageValidation.Models.Requests;
using Synapsis.Sil.Services.Descriptions;
using Synapsis.Sil.Services.Schemas;
using YamlDotNet.Core;

namespace Synapsis.Sil.PackageValidation.Services.PackageRules;

/// <summary>
/// The package must match the package format (schemas/sil/schema-package.schema.json): its required sections, the
/// form of every code and name, no unknown fields.
/// </summary>
public sealed class FormatRule : IPackageRule
{
    /// <summary>The package format, prepared once.</summary>
    private static readonly JsonSchema PackageSchema = JsonSchema.FromText(DescriptionFiles.ReadText(DescriptionFiles.PackageSchema));

    /// <summary>Passes when the package matches the format; E-SCHEMA-PACKAGE at the problem otherwise.</summary>
    public Result Check(PackageValidationRequest request)
    {
        JsonNode? json;
        try
        {
            json = YamlJson.ToJson(request.Text);
        }
        catch (YamlException problem)
        {
            return Result.Failure(ErrorCodes.SchemaPackage, string.Empty, "the package is not YAML or JSON: " + problem.Message);
        }
        JsonElement package = JsonSerializer.SerializeToElement(json);
        return SchemaEvaluation.Check(PackageSchema, package, ErrorCodes.SchemaPackage, "the package does not match the package format");
    }
}
