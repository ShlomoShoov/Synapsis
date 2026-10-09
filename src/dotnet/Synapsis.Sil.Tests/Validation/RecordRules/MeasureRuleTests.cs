using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 9: the measures.</summary>
public class MeasureRuleTests
{
    /// <summary>A measure the type does not allow is E-MEASURE.</summary>
    [Fact]
    public void A_measure_the_type_does_not_allow_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["measures"] = Measures(("deaths_best", 3, "count"));
        RuleCheck.Refused(record, ErrorCodes.Measure, "measures[0].name");
    }

    /// <summary>The same measure twice is E-MEASURE at the second one.</summary>
    [Fact]
    public void A_repeated_measure_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["measures"] = Measures(("magnitude", 5, "magnitude"), ("magnitude", 6, "magnitude"));
        RuleCheck.Refused(record, ErrorCodes.Measure, "measures[1].name");
    }

    /// <summary>Another unit than the registered one is E-UNIT.</summary>
    [Fact]
    public void A_wrong_unit_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["measures"] = Measures(("depth_km", 10000, "m"));
        RuleCheck.Refused(record, ErrorCodes.Unit, "measures[0].unit");
    }

    /// <summary>A negative value for an unsigned measure is E-MEASURE-NEGATIVE.</summary>
    [Fact]
    public void A_negative_unsigned_value_is_refused()
    {
        JsonObject record = TestRecords.BankQuarter();
        record["measures"] = Measures(("assets", -5, "usd_k"));
        RuleCheck.Refused(record, ErrorCodes.MeasureNegative, "measures[0].value");
    }

    /// <summary>A value beyond the measure's range (magnitude at most 10) is E-MEASURE-RANGE.</summary>
    [Fact]
    public void A_value_out_of_range_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["measures"] = Measures(("magnitude", 11, "magnitude"));
        RuleCheck.Refused(record, ErrorCodes.MeasureRange, "measures[0].value");
    }

    /// <summary>A list of measures (name, value, unit).</summary>
    private static JsonArray Measures(params (string Name, double Value, string Unit)[] measures)
    {
        JsonArray list = new JsonArray();
        foreach ((string Name, double Value, string Unit) measure in measures)
        {
            list.Add(new JsonObject { ["name"] = measure.Name, ["value"] = measure.Value, ["unit"] = measure.Unit });
        }
        return list;
    }
}
