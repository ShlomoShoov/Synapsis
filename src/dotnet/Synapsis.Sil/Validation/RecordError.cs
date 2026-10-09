namespace Synapsis.Sil.Validation;

/// <summary>
/// One broken rule: its error code, where in the record it was found, and a sentence for people.
/// The first rule a record breaks decides its error, so a record has at most one.
/// </summary>
public sealed class RecordError
{
    /// <summary>Creates an error.</summary>
    public RecordError(string code, string path, string message)
    {
        Code = code;
        Path = path;
        Message = message;
    }

    /// <summary>The stable error code, one of <see cref="ErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>
    /// Where the problem is, written like "measures[2].value" or "place.logical.path[0]"; empty means the record itself.
    /// </summary>
    public string Path { get; }

    /// <summary>What is wrong, in plain words.</summary>
    public string Message { get; }
}
