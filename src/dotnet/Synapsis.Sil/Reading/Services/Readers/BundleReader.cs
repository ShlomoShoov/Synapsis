using System.Text.Json;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Reading.Models.Configuration;

namespace Synapsis.Sil.Reading.Services.Readers;

/// <summary>Reads a request body into an <see cref="IngestBundle"/> with System.Text.Json.</summary>
public sealed class BundleReader : IBundleReader
{
    /// <summary>The bundle, or E-BUNDLE when the text is not valid JSON or does not fit the bundle class.</summary>
    public Result<IngestBundle> Read(string json)
    {
        try
        {
            IngestBundle? bundle = JsonSerializer.Deserialize<IngestBundle>(json, SilJsonOptions.Value);
            if (bundle == null)
            {
                return Result<IngestBundle>.Failure(ErrorCodes.Bundle, string.Empty, "the body is JSON null, not a bundle");
            }
            return Result<IngestBundle>.Success(bundle);
        }
        catch (JsonException problem)
        {
            return Result<IngestBundle>.Failure(ErrorCodes.Bundle, string.Empty, problem.Message);
        }
    }
}
