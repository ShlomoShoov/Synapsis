using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Domain.Vocabulary.Attributes;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models.Requests;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 11: every attribute must be declared by the record's type and respect its declaration (type, length, items,
/// allowed values, pattern, range), and every required attribute must be present.
/// </summary>
public sealed class AttributeRule : IRecordRule
{
    /// <summary>Passes, or fails with E-ATTRIBUTE at "attributes.&lt;name&gt;".</summary>
    public Result Check(RecordContext context)
    {
        SilRecord record = context.Record;
        IReadOnlyDictionary<string, AttributeDefinition> declared = context.Registry.AttributesOf(record.Kind, record.Type);
        Dictionary<string, JsonElement> sent = record.Attributes ?? new Dictionary<string, JsonElement>();
        foreach (KeyValuePair<string, JsonElement> attribute in sent)
        {
            string path = "attributes." + attribute.Key;
            if (!declared.TryGetValue(attribute.Key, out AttributeDefinition? definition))
            {
                return Result.Failure(ErrorCodes.Attribute, path, $"attribute '{attribute.Key}' is not declared for this type");
            }
            string? problem = AttributeValueCheck.Problem(attribute.Value, definition, context.Registry);
            if (problem != null)
            {
                return Result.Failure(ErrorCodes.Attribute, path, problem);
            }
        }
        return CheckRequired(declared, sent);
    }

    /// <summary>Every attribute declared as required is present.</summary>
    private static Result CheckRequired(IReadOnlyDictionary<string, AttributeDefinition> declared, Dictionary<string, JsonElement> sent)
    {
        foreach (KeyValuePair<string, AttributeDefinition> attribute in declared)
        {
            if (attribute.Value.Required && !sent.ContainsKey(attribute.Key))
            {
                return Result.Failure(ErrorCodes.Attribute, "attributes." + attribute.Key, $"attribute '{attribute.Key}' is required");
            }
        }
        return Result.Success();
    }
}
