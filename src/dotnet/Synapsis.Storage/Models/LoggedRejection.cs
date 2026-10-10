namespace Synapsis.Storage.Models;

/// <summary>One refused record, as the ingestion log keeps it: where it was in the batch and why it was refused.</summary>
public sealed class LoggedRejection
{
    /// <summary>The record's position in the batch, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>The record's id, when it had a readable one.</summary>
    public string? Id { get; init; }

    /// <summary>The error code, such as "E-UNIT".</summary>
    public required string Code { get; init; }

    /// <summary>Where in the record the problem is, such as "measures[2].unit".</summary>
    public required string Path { get; init; }
}
