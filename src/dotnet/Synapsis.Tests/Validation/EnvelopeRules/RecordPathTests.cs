using Synapsis.Sil.Validation.Services.EnvelopeRules;
using Xunit;

namespace Synapsis.Tests.Validation.EnvelopeRules;

/// <summary>Checks the conversion of JSON Pointer locations into the contract's path format.</summary>
public class RecordPathTests
{
    /// <summary>Names are joined by dots and list positions go in brackets.</summary>
    [Theory]
    [InlineData("/measures/2/value", "measures[2].value")]
    [InlineData("/place/logical/path/0", "place.logical.path[0]")]
    [InlineData("/known_at", "known_at")]
    [InlineData("", "")]
    public void A_json_pointer_becomes_a_dotted_path(string pointer, string expected)
    {
        Assert.Equal(expected, RecordPath.FromJsonPointer(pointer));
    }

    /// <summary>Escaped characters in a pointer (~1 for "/", ~0 for "~") are restored.</summary>
    [Fact]
    public void Escaped_characters_are_restored()
    {
        Assert.Equal("attributes.a/b", RecordPath.FromJsonPointer("/attributes/a~1b"));
    }
}
