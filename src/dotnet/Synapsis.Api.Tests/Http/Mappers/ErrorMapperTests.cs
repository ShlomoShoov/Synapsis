using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Synapsis.Api.Http.Models.Configuration;
using Synapsis.Api.Http.Services.Mappers;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Xunit;

namespace Synapsis.Api.Tests.Http.Mappers;

/// <summary>Every refused request leaves as {code, message, details} with its status.</summary>
public class ErrorMapperTests
{
    /// <summary>A problem with a place gives one detail; the status comes from the code.</summary>
    [Fact]
    public void A_problem_becomes_the_error_body_and_status()
    {
        ObjectResult result = ErrorMapper.ToResult(new Problem(ErrorCodes.TooLarge, "records", "a batch holds at most 5000 records"));
        Assert.Equal(413, result.StatusCode);
        JsonElement body = JsonSerializer.SerializeToElement(result.Value, ApiJson.Options);
        Assert.Equal("E-TOO-LARGE", body.GetProperty("code").GetString());
        Assert.Equal("records", body.GetProperty("details")[0].GetProperty("path").GetString());
    }

    /// <summary>A problem without a place has no details field at all.</summary>
    [Fact]
    public void A_problem_without_a_place_has_no_details()
    {
        JsonElement body = JsonSerializer.SerializeToElement(ErrorMapper.ToDto(new Problem(ErrorCodes.Auth, "", "key required")), ApiJson.Options);
        Assert.False(body.TryGetProperty("details", out _));
    }
}
