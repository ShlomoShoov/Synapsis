using System;
using Synapsis.Api.Http.Services.Readers;
using Synapsis.Sil.Models.Descriptions;
using Xunit;

namespace Synapsis.Api.Tests.Http.Readers;

/// <summary>The forms of as_of.</summary>
public class AsOfReaderTests
{
    /// <summary>No value means the latest.</summary>
    [Fact]
    public void No_value_means_the_latest()
    {
        Assert.Null(AsOfReader.Read(null).Value);
        Assert.Null(AsOfReader.Read("").Value);
    }

    /// <summary>A date means the end of that day, UTC.</summary>
    [Fact]
    public void A_date_means_the_end_of_that_day()
    {
        DateTime moment = AsOfReader.Read("2024-05-01").Value!.Value;
        Assert.Equal(new DateTime(2024, 5, 1, 23, 59, 59, 999, DateTimeKind.Utc), moment);
        Assert.Equal(DateTimeKind.Utc, moment.Kind);
    }

    /// <summary>A SIL instant is taken as given.</summary>
    [Fact]
    public void An_instant_is_taken_as_given()
    {
        Assert.Equal(new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc), AsOfReader.Read("2024-05-01T12:00:00Z").Value);
    }

    /// <summary>Anything else is E-PARAM at as_of.</summary>
    [Theory]
    [InlineData("yesterday")]
    [InlineData("2024-05-01T12:00:00+02:00")]
    [InlineData("2024-02-30")]
    public void Anything_else_is_refused(string text)
    {
        Assert.Equal(ErrorCodes.Param, AsOfReader.Read(text).Problem!.Code);
        Assert.Equal("as_of", AsOfReader.Read(text).Problem!.Path);
    }
}
