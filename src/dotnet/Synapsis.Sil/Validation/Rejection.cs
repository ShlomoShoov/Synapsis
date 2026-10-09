namespace Synapsis.Sil.Validation;

/// <summary>
/// A refused record as it appears in the receipt: its position in the batch, its id, and the error that refused it.
/// </summary>
public sealed class Rejection
{
    /// <summary>Creates a rejection for the record at <paramref name="index"/>.</summary>
    public Rejection(int index, string? id, RecordError error)
    {
        Index = index;
        Id = id;
        Code = error.Code;
        Path = error.Path;
        Message = error.Message;
    }

    /// <summary>The record's position in the batch, counting from 0.</summary>
    public int Index { get; }

    /// <summary>The record's id, when it had one.</summary>
    public string? Id { get; }

    /// <summary>The error code.</summary>
    public string Code { get; }

    /// <summary>Where in the record the problem is.</summary>
    public string Path { get; }

    /// <summary>What is wrong, in plain words.</summary>
    public string Message { get; }
}
