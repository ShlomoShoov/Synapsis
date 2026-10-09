using System.Text.Json;
using Json.Schema;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Reading.Models.Configuration;
using Synapsis.Sil.Services.Descriptions;
using Synapsis.Sil.Services.Schemas;

namespace Synapsis.Sil.Reading.Services.Readers;

/// <summary>
/// Reads a request body into an <see cref="IngestBundle"/> in three steps: it must be JSON; it may hold at most 5,000
/// records (checked before the envelope, so an oversized batch answers E-TOO-LARGE and not E-BUNDLE); its envelope must
/// match the bundle part of the record language's schema (sil "1.2", a batch id, a producer, a list of objects).
/// </summary>
public sealed class BundleReader : IBundleReader
{
    /// <summary>The most records one batch may hold.</summary>
    private const int MaxRecords = 5000;

    /// <summary>The bundle's schema: the root of the record language's schema file, prepared once.</summary>
    private static readonly JsonSchema BundleSchema = JsonSchema.FromText(DescriptionFiles.ReadText(DescriptionFiles.RecordSchema));

    /// <inheritdoc/>
    public Result<IngestBundle> Read(string json)
    {
        Result<JsonElement> parsed = Parse(json);
        if (!parsed.Succeeded)
        {
            return Result<IngestBundle>.Failure(parsed.Problem!);
        }
        JsonElement body = parsed.Value;
        if (RecordCount(body) > MaxRecords)
        {
            return Result<IngestBundle>.Failure(ErrorCodes.TooLarge, "records", $"a batch holds at most {MaxRecords} records");
        }
        Result envelope = SchemaEvaluation.Check(BundleSchema, body, ErrorCodes.Bundle, "the bundle envelope is invalid");
        if (!envelope.Succeeded)
        {
            return Result<IngestBundle>.Failure(envelope.Problem!);
        }
        return ToBundle(body);
    }

    /// <summary>The body as JSON, or E-BUNDLE when it is not JSON (NaN is not JSON either).</summary>
    private static Result<JsonElement> Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Result<JsonElement>.Success(document.RootElement.Clone());
        }
        catch (JsonException problem)
        {
            return Result<JsonElement>.Failure(ErrorCodes.Bundle, string.Empty, "the body is not JSON: " + problem.Message);
        }
    }

    /// <summary>How many items the body's "records" list holds; 0 when there is no such list.</summary>
    private static int RecordCount(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("records", out JsonElement records))
        {
            return 0;
        }
        if (records.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }
        return records.GetArrayLength();
    }

    /// <summary>The checked envelope as an <see cref="IngestBundle"/>.</summary>
    private static Result<IngestBundle> ToBundle(JsonElement body)
    {
        try
        {
            IngestBundle bundle = body.Deserialize<IngestBundle>(SilJsonOptions.Value)!;
            return Result<IngestBundle>.Success(bundle);
        }
        catch (JsonException problem)
        {
            return Result<IngestBundle>.Failure(ErrorCodes.Bundle, string.Empty, problem.Message);
        }
    }
}
