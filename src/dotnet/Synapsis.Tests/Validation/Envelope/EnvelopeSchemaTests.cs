using System.Text.Json.Nodes;
using Synapsis.Sil.Records;
using Synapsis.Sil.Validation;
using Synapsis.Sil.Validation.Envelope;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.Envelope;

/// <summary>Checks rule 2: the record's structure against the record language's JSON Schema.</summary>
public class EnvelopeSchemaTests
{
    /// <summary>A valid record passes.</summary>
    [Fact]
    public void A_valid_record_passes()
    {
        Assert.Null(EnvelopeSchema.Check(RecordKind.Event, TestRecords.ToElement(TestRecords.Earthquake())));
    }

    /// <summary>A field the record language does not have is E-SCHEMA.</summary>
    [Fact]
    public void An_undeclared_field_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["severity"] = 3;
        Assert.Equal(ErrorCodes.Schema, Check(record).Code);
    }

    /// <summary>A missing required field is E-SCHEMA, located at the record itself (the empty path).</summary>
    [Fact]
    public void A_missing_required_field_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record.Remove("known_at");
        RecordError error = Check(record);
        Assert.Equal(ErrorCodes.Schema, error.Code);
        Assert.Equal(string.Empty, error.Path);
    }

    /// <summary>A value of the wrong type or out of range is E-SCHEMA, located at the value.</summary>
    [Fact]
    public void A_wrong_value_is_refused_at_its_location()
    {
        JsonObject record = TestRecords.Earthquake();
        record["reliability"] = 101;
        RecordError error = Check(record);
        Assert.Equal(ErrorCodes.Schema, error.Code);
        Assert.Equal("reliability", error.Path);
    }

    /// <summary>An instant with an offset instead of Z is E-SCHEMA.</summary>
    [Fact]
    public void An_instant_with_an_offset_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["known_at"] = "2024-01-01T08:10:09+00:00";
        Assert.Equal(ErrorCodes.Schema, Check(record).Code);
    }

    /// <summary>Runs the check and expects a failure.</summary>
    private static RecordError Check(JsonObject record)
    {
        RecordError? error = EnvelopeSchema.Check(RecordKind.Event, TestRecords.ToElement(record));
        Assert.NotNull(error);
        return error;
    }
}
