using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Api.Models;
using Synapsis.Api.Models.Records;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Services;

/// <summary>
/// Step 1 of ingestion, validation. Each record in turn is read as the class of its kind, checked for the fields every
/// record of that kind needs, and compared with the records before it in the same batch.
/// </summary>
public static class RecordValidator
{
    /// <summary>The record cannot be read: not an object, an unknown kind, or a field of the wrong type (such as a known_at that is not a date-time).</summary>
    public const string CannotRead = "E-JSON";

    /// <summary>A required field is missing or empty.</summary>
    public const string MissingField = "E-MISSING";

    /// <summary>The same id from the same source appears earlier in the batch with other content.</summary>
    public const string DuplicateId = "E-DUPLICATE-ID";

    /// <summary>Splits the batch's records into valid, refused and exact copies.</summary>
    public static ValidationResult Validate(IReadOnlyList<JsonElement> records)
    {
        ValidationResult result = new ValidationResult();
        Dictionary<string, string> firstTextByKey = new Dictionary<string, string>();
        for (int index = 0; index < records.Count; index++)
        {
            JsonElement json = records[index];

            // 1. Read the record as the class of its kind (E-JSON when it cannot be read).
            SilRecord? record;
            try
            {
                record = RecordReader.Read(json);
            }
            catch (JsonException problem)
            {
                result.Rejected.Add(Reject(json, index, CannotRead, problem.Message));
                continue;
            }
            record.Index = index;
            record.Json = json;

            // 2. Check the required fields.
            string? missing = FindMissingField(record);
            if (missing != null)
            {
                result.Rejected.Add(Reject(json, index, MissingField, missing + " is missing"));
                continue;
            }

            // 3. Compare with the records before it: the same id from the same source may appear only once.
            string key = StoredRecord.KeyOf(record.Id!, record.Source!);
            string text = json.GetRawText();
            if (firstTextByKey.TryGetValue(key, out string? firstText))
            {
                if (firstText == text)
                {
                    result.Duplicates.Add(record);
                }
                else
                {
                    result.Rejected.Add(Reject(json, index, DuplicateId, "the same id and source appear earlier in the batch with other content"));
                }
                continue;
            }
            firstTextByKey.Add(key, text);
            result.Valid.Add(record);
        }
        return result;
    }

    /// <summary>The name of the first required field that is missing or empty, or null when none is.</summary>
    private static string? FindMissingField(SilRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Id))
        {
            return "id";
        }
        if (string.IsNullOrWhiteSpace(record.Type))
        {
            return "type";
        }
        if (string.IsNullOrWhiteSpace(record.Source))
        {
            return "source";
        }
        if (record.KnownAt == null)
        {
            return "known_at";
        }
        return FindMissingFieldOfKind(record);
    }

    /// <summary>The first missing field that only this kind of record needs, or null.</summary>
    private static string? FindMissingFieldOfKind(SilRecord record)
    {
        switch (record)
        {
            case EntityRecord entity when string.IsNullOrWhiteSpace(entity.Name):
                return "name";
            case EventRecord eventRecord when eventRecord.Time == null || eventRecord.Time.Start == null:
                return "time.start";
            case RelationRecord relation when string.IsNullOrWhiteSpace(relation.From):
                return "from";
            case RelationRecord relation when string.IsNullOrWhiteSpace(relation.To):
                return "to";
            case ConclusionRecord conclusion when conclusion.About == null:
                return "about";
            case ConclusionRecord conclusion when conclusion.Statement == null:
                return "statement";
            default:
                return null;
        }
    }

    /// <summary>A refused record: its position, its id and source when they can be read, the code and the reason.</summary>
    private static RejectedRecord Reject(JsonElement json, int index, string code, string reason)
    {
        RejectedRecord rejected = new RejectedRecord();
        rejected.Index = index;
        rejected.Id = RecordReader.ReadText(json, "id");
        rejected.Source = RecordReader.ReadText(json, "source") ?? "unknown";
        rejected.Code = code;
        rejected.Reason = reason;
        return rejected;
    }
}
