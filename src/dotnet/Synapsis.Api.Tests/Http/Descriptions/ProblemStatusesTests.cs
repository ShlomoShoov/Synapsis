using Synapsis.Api.Http.Models.Descriptions;
using Synapsis.Sil.Models.Descriptions;
using Xunit;

namespace Synapsis.Api.Tests.Http.Descriptions;

/// <summary>The status of each request-level code, as the contract fixes it.</summary>
public class ProblemStatusesTests
{
    /// <summary>Each code answers its status.</summary>
    [Theory]
    [InlineData(ErrorCodes.Bundle, 400)]
    [InlineData(ErrorCodes.Param, 400)]
    [InlineData(ErrorCodes.Auth, 401)]
    [InlineData(ErrorCodes.NotFound, 404)]
    [InlineData(ErrorCodes.SchemaOwnership, 409)]
    [InlineData(ErrorCodes.SchemaBreaking, 409)]
    [InlineData(ErrorCodes.SchemaVersion, 409)]
    [InlineData(ErrorCodes.TooLarge, 413)]
    [InlineData(ErrorCodes.SchemaPackage, 422)]
    [InlineData(ErrorCodes.SchemaRef, 422)]
    [InlineData(ErrorCodes.Unavailable, 503)]
    public void Each_code_answers_its_status(string code, int status)
    {
        Assert.Equal(status, ProblemStatuses.Of(code));
    }

    /// <summary>A record code never answers a request; if it ever does, it is a bug (500).</summary>
    [Fact]
    public void A_record_code_is_not_a_request_answer()
    {
        Assert.Equal(500, ProblemStatuses.Of(ErrorCodes.MeasureNegative));
    }
}
