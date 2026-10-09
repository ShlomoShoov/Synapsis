using System;
using Synapsis.Sil.Validation.Services.Time;
using Xunit;

namespace Synapsis.Tests.Validation.Time;

/// <summary>Checks the alignment of a start to each of the eight precisions.</summary>
public class PrecisionAlignmentTests
{
    /// <summary>A start on the boundary of its precision is aligned; one inside the period is not.</summary>
    [Theory]
    [InlineData("second", "2024-05-17T13:42:07Z", "2024-05-17T13:42:07.5Z")]
    [InlineData("minute", "2024-05-17T13:42:00Z", "2024-05-17T13:42:07Z")]
    [InlineData("hour", "2024-05-17T13:00:00Z", "2024-05-17T13:42:00Z")]
    [InlineData("day", "2024-05-17T00:00:00Z", "2024-05-17T13:00:00Z")]
    [InlineData("week", "2024-05-13T00:00:00Z", "2024-05-17T00:00:00Z")]
    [InlineData("month", "2024-05-01T00:00:00Z", "2024-05-17T00:00:00Z")]
    [InlineData("quarter", "2024-04-01T00:00:00Z", "2024-05-01T00:00:00Z")]
    [InlineData("year", "2024-01-01T00:00:00Z", "2024-04-01T00:00:00Z")]
    public void Each_precision_has_its_boundary(string precision, string aligned, string inside)
    {
        Assert.True(PrecisionAlignment.IsAligned(Parse(aligned), precision));
        Assert.False(PrecisionAlignment.IsAligned(Parse(inside), precision));
    }

    /// <summary>An unknown precision is never aligned.</summary>
    [Fact]
    public void An_unknown_precision_is_never_aligned()
    {
        Assert.False(PrecisionAlignment.IsAligned(Parse("2024-01-01T00:00:00Z"), "decade"));
    }

    /// <summary>Reads an instant for the test.</summary>
    private static DateTime Parse(string text)
    {
        Assert.True(Instant.TryParse(text, out DateTime instant));
        return instant;
    }
}
