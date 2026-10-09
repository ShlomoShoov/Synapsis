using System;
using Synapsis.Sil.Services.Time;
using Xunit;

namespace Synapsis.Sil.Tests.Time;

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

    /// <summary>Whole seconds are written without a fraction; a fraction is written to at most six digits.</summary>
    [Fact]
    public void An_instant_is_written_in_the_SIL_format()
    {
        DateTime whole = new DateTime(2026, 10, 9, 8, 15, 0, DateTimeKind.Utc);
        Assert.Equal("2026-10-09T08:15:00Z", Instant.Format(whole));
        Assert.Equal("2026-10-09T08:15:00.25Z", Instant.Format(whole.AddMilliseconds(250)));
        Assert.Equal("2026-10-09T08:15:00.000001Z", Instant.Format(whole.AddTicks(15)));
    }

    /// <summary>What Format writes, TryParse reads back (to the microsecond).</summary>
    [Fact]
    public void A_written_instant_reads_back()
    {
        DateTime moment = new DateTime(2026, 10, 9, 8, 15, 7, DateTimeKind.Utc).AddTicks(1234560);
        Assert.True(Instant.TryParse(Instant.Format(moment), out DateTime read));
        Assert.Equal(moment, read);
    }
}
