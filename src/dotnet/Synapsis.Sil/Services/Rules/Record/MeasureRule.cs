using System.Collections.Generic;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Models.Validation;
namespace Synapsis.Sil.Services.Rules.Record;

/// <summary>
/// Rule 9 (events): each measure must be allowed by the event type and appear once, in its registered unit, never
/// negative unless the measure is signed, and within its range. Values are never corrected: a broken one is refused.
/// </summary>
public sealed class MeasureRule : IRecordRule
{
    /// <summary>Passes (always for other kinds), or fails with E-MEASURE, E-UNIT, E-MEASURE-NEGATIVE or E-MEASURE-RANGE.</summary>
    public Result Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord || eventRecord.Measures == null)
        {
            return Result.Success();
        }
        EventTypeDefinition type = context.Registry.FindEventType(eventRecord.Type)!;
        HashSet<string> seen = new HashSet<string>();
        for (int index = 0; index < eventRecord.Measures.Count; index++)
        {
            Measure measure = eventRecord.Measures[index];
            if (!type.Measures.Contains(measure.Name) || seen.Contains(measure.Name))
            {
                return Result.Failure(ErrorCodes.Measure, $"measures[{index}].name", $"measure '{measure.Name}' is not allowed for '{type.Code}' or repeats");
            }
            seen.Add(measure.Name);
            Result result = CheckValue(measure, context.Registry.FindMeasure(measure.Name)!, index);
            if (!result.Succeeded)
            {
                return result;
            }
        }
        return Result.Success();
    }

    /// <summary>The unit, the sign and the range of one measure.</summary>
    private static Result CheckValue(Measure measure, MeasureDefinition definition, int index)
    {
        if (measure.Unit != definition.Unit)
        {
            return Result.Failure(ErrorCodes.Unit, $"measures[{index}].unit", $"'{measure.Name}' is measured in '{definition.Unit}'");
        }
        if (measure.Value < 0 && !definition.Signed)
        {
            return Result.Failure(ErrorCodes.MeasureNegative, $"measures[{index}].value", $"'{measure.Name}' cannot be negative");
        }
        bool belowMin = definition.Min != null && measure.Value < definition.Min;
        bool aboveMax = definition.Max != null && measure.Value > definition.Max;
        if (belowMin || aboveMax)
        {
            return Result.Failure(ErrorCodes.MeasureRange, $"measures[{index}].value", $"'{measure.Name}' must lie in [{definition.Min}, {definition.Max}]");
        }
        return Result.Success();
    }
}
