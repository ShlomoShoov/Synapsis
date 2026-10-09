using System;
using Synapsis.Sil.Validation.Services.Time;
using Xunit;

namespace Synapsis.Tests.Validation.Time;

/// <summary>Checks the one accepted time format.</summary>
public class InstantTests
{
    /// <summary>A UTC instant, with or without a fraction of a second, is read.</summary>
    [Fact]
    public void A_utc_instant_is_read()
    {
        Assert.True(Instant.TryParse("2023-06-02T07:10:09Z", out DateTime whole));
        Assert.Equal(new DateTime(2023, 6, 2, 7, 10, 9, DateTimeKind.Utc), whole);
        Assert.True(Instant.TryParse("2023-06-02T07:10:09.5Z", out DateTime fraction));
        Assert.Equal(whole.AddMilliseconds(500), fraction);
    }

    /// <summary>Other formats and impossible moments are refused.</summary>
    [Theory]
    [InlineData("2023-06-02")]
    [InlineData("2023-06-02T07:10:09+02:00")]
    [InlineData("2023-06-02 07:10:09Z")]
    [InlineData("2024-02-30T00:00:00Z")]
    [InlineData("2023-06-02T24:00:00Z")]
    [InlineData("2023-06-02T07:10:09.1234567Z")]
    public void Other_formats_and_impossible_moments_are_refused(string text)
    {
        Assert.False(Instant.TryParse(text, out DateTime _));
    }
}
