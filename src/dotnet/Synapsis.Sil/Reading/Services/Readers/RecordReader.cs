using System.Text.Json;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Reading.Models.Configuration;

namespace Synapsis.Sil.Reading.Services.Readers;

/// <summary>
/// Reads a raw record into the class of its kind with System.Text.Json: a plain switch on the "kind" field chooses
/// <see cref="EntityRecord"/>, <see cref="EventRecord"/>, <see cref="RelationRecord"/> or <see cref="ConclusionRecord"/>.
/// </summary>
public sealed class RecordReader : IRecordReader
{
    /// <summary>
    /// The typed record; E-KIND when the kind is unknown; E-SCHEMA when a value does not fit its C# type (a
    /// reliability written as 95.0) or a field is unknown.
    /// </summary>
    public Result<SilRecord> Read(JsonElement raw)
    {
        try
        {
            switch (KindOf(raw))
            {
                case "entity":
                    return ReadAs<EntityRecord>(raw);
                case "event":
                    return ReadAs<EventRecord>(raw);
                case "relation":
                    return ReadAs<RelationRecord>(raw);
                case "conclusion":
                    return ReadAs<ConclusionRecord>(raw);
                default:
                    return Result<SilRecord>.Failure(ErrorCodes.Kind, "kind", "kind must be entity, event, relation or conclusion");
            }
        }
        catch (JsonException problem)
        {
            return Result<SilRecord>.Failure(ErrorCodes.Schema, string.Empty, problem.Message);
        }
    }

    /// <summary>Reads the record into the given class.</summary>
    private static Result<SilRecord> ReadAs<T>(JsonElement raw) where T : SilRecord
    {
        T? typed = raw.Deserialize<T>(SilJsonOptions.Value);
        if (typed == null)
        {
            return Result<SilRecord>.Failure(ErrorCodes.Schema, string.Empty, "the record is JSON null");
        }
        return Result<SilRecord>.Success(typed);
    }

    /// <summary>The record's "kind" text, or an empty text when there is none.</summary>
    private static string KindOf(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }
        if (!raw.TryGetProperty("kind", out JsonElement kind) || kind.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return kind.GetString() ?? string.Empty;
    }
}
