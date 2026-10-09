using System.Collections.Generic;

namespace Synapsis.Api.Http.Models.Dtos;

/// <summary>The body of every refused request: {"code", "message", "details"}.</summary>
public sealed class ErrorDto
{
    /// <summary>The error code, for example "E-BUNDLE".</summary>
    public required string Code { get; init; }

    /// <summary>What went wrong, for people.</summary>
    public required string Message { get; init; }

    /// <summary>Where it went wrong, when the problem has a place; left out otherwise.</summary>
    public List<ErrorDetailDto>? Details { get; init; }
}
