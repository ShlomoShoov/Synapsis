namespace Synapsis.Api.Models.Dtos;

/// <summary>One refused record of a receipt: its place in the batch, its id, and the broken rule.</summary>
public sealed class RejectionDto
{
    /// <summary>Its place in the batch, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>Its id, when it had one as text.</summary>
    public string? Id { get; init; }

    /// <summary>The error code, for example "E-MEASURE-NEGATIVE".</summary>
    public required string Code { get; init; }

    /// <summary>Where in the record, for example "measures[2].value"; "" is the record itself.</summary>
    public required string Path { get; init; }

    /// <summary>What is wrong, for people.</summary>
    public required string Message { get; init; }
}
