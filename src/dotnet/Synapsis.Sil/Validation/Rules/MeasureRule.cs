using System.Collections.Generic;
using Synapsis.Sil.Records;
using Synapsis.Sil.Records.Parts;
using Synapsis.Sil.Vocabulary.Terms;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 9 (events): each measure must be allowed by the event type and appear once, in its registered unit, never
/// negative unless the measure is signed, and within its range. Values are never corrected: a broken one is refused.
/// </summary>
public sealed class MeasureRule : IRecordRule
{
    /// <summary>Returns E-MEASURE, E-UNIT, E-MEASURE-NEGATIVE, E-MEASURE-RANGE, or null. Other kinds pass.</summary>
    public RecordError? Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord || eventRecord.Measures == null)
        {
            return null;
        }
        EventTypeDefinition type = context.Registry.EventTypes[eventRecord.Type];
        HashSet<string> seen = new HashSet<string>();
        for (int index = 0; index < eventRecord.Measures.Count; index++)
        {
            Measure measure = eventRecord.Measures[index];
            if (!type.Measures.Contains(measure.Name) || seen.Contains(measure.Name))
            {
                return new RecordError(ErrorCodes.Measure, $"measures[{index}].name",
                    $"measure '{measure.Name}' is not allowed for '{type.Code}' or repeats");
            }
            seen.Add(measure.Name);
            RecordError? error = CheckValue(measure, context.Registry.Measures[measure.Name], index);
            if (error != null)
            {
                return error;
            }
        }
        return null;
    }

    /// <summary>The unit, the sign and the range of one measure.</summary>
    private static RecordError? CheckValue(Measure measure, MeasureDefinition definition, int index)
    {
        if (measure.Unit != definition.Unit)
        {
            return new RecordError(ErrorCodes.Unit, $"measures[{index}].unit",
                $"'{measure.Name}' is measured in '{definition.Unit}'");
        }
        if (measure.Value < 0 && !definition.Signed)
        {
            return new RecordError(ErrorCodes.MeasureNegative, $"measures[{index}].value",
                $"'{measure.Name}' cannot be negative");
        }
        bool belowMin = definition.Min != null && measure.Value < definition.Min;
        bool aboveMax = definition.Max != null && measure.Value > definition.Max;
        if (belowMin || aboveMax)
        {
            return new RecordError(ErrorCodes.MeasureRange, $"measures[{index}].value",
                $"'{measure.Name}' must lie in [{definition.Min}, {definition.Max}]");
        }
        return null;
    }
}
