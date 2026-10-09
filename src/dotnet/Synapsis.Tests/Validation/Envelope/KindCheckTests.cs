using System.Text.Json.Nodes;
using Synapsis.Sil.Records;
using Synapsis.Sil.Validation;
using Synapsis.Sil.Validation.Envelope;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.Envelope;

/// <summary>Checks rule 1: the record kind.</summary>
public class KindCheckTests
{
    /// <summary>The four kinds are accepted and named.</summary>
    [Fact]
    public void A_known_kind_passes()
    {
        RecordError? error = KindCheck.Check(TestRecords.ToElement(TestRecords.Earthquake()), out RecordKind kind);
        Assert.Null(error);
        Assert.Equal(RecordKind.Event, kind);
    }

    /// <summary>A source sent as a record, or no kind at all, is E-KIND at "kind".</summary>
    [Theory]
    [InlineData("source")]
    [InlineData(null)]
    public void An_unknown_or_missing_kind_is_refused(string? kindText)
    {
        JsonObject record = TestRecords.Earthquake();
        record["kind"] = kindText;
        RecordError? error = KindCheck.Check(TestRecords.ToElement(record), out RecordKind _);
        Assert.Equal(ErrorCodes.Kind, error!.Code);
        Assert.Equal("kind", error.Path);
    }
}
