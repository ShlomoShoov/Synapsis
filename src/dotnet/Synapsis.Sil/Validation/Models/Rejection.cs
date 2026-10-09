using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Validation.Models;

/// <summary>A refused record as it appears in the receipt: its position in the batch, its id, and its problem.</summary>
public sealed class Rejection
{
    /// <summary>Creates a rejection.</summary>
    public Rejection(int index, string? id, Problem problem)
    {
        Index = index;
        Id = id;
        Problem = problem;
    }

    /// <summary>The record's position in the batch, counting from 0.</summary>
    public int Index { get; }

    /// <summary>The record's id, when it had one.</summary>
    public string? Id { get; }

    /// <summary>Why it was refused: code, path, message.</summary>
    public Problem Problem { get; }
}
