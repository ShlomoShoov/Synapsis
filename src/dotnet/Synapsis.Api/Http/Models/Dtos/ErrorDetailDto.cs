namespace Synapsis.Api.Http.Models.Dtos;

/// <summary>One place of a refused request: a path in the body or a parameter name, and what is wrong there.</summary>
public sealed class ErrorDetailDto
{
    /// <summary>The place, for example "records" or "as_of".</summary>
    public required string Path { get; init; }

    /// <summary>What is wrong there.</summary>
    public required string Message { get; init; }
}
