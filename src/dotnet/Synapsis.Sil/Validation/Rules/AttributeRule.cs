using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil.Records;
using Synapsis.Sil.Vocabulary.Attributes;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 11: every attribute must be declared by the record's type and respect its declaration (type, length, items,
/// allowed values, pattern, range), and every required attribute must be present.
/// </summary>
public sealed class AttributeRule : IRecordRule
{
    /// <summary>Returns E-ATTRIBUTE at "attributes.&lt;name&gt;", or null.</summary>
    public RecordError? Check(RecordContext context)
    {
        SilRecord record = context.Record;
        IReadOnlyDictionary<string, AttributeDefinition> declared = context.Registry.AttributesOf(record.Kind, record.Type);
        Dictionary<string, JsonElement> sent = record.Attributes ?? new Dictionary<string, JsonElement>();
        foreach (KeyValuePair<string, JsonElement> attribute in sent)
        {
            string path = "attributes." + attribute.Key;
            if (!declared.TryGetValue(attribute.Key, out AttributeDefinition? definition))
            {
                return new RecordError(ErrorCodes.Attribute, path, $"attribute '{attribute.Key}' is not declared for this type");
            }
            string? problem = AttributeValueCheck.Problem(attribute.Value, definition, context.Registry);
            if (problem != null)
            {
                return new RecordError(ErrorCodes.Attribute, path, problem);
            }
        }
        return CheckRequired(declared, sent);
    }

    /// <summary>Every attribute declared as required is present.</summary>
    private static RecordError? CheckRequired(IReadOnlyDictionary<string, AttributeDefinition> declared,
        Dictionary<string, JsonElement> sent)
    {
        foreach (KeyValuePair<string, AttributeDefinition> attribute in declared)
        {
            if (attribute.Value.Required && !sent.ContainsKey(attribute.Key))
            {
                return new RecordError(ErrorCodes.Attribute, "attributes." + attribute.Key,
                    $"attribute '{attribute.Key}' is required");
            }
        }
        return null;
    }
}
