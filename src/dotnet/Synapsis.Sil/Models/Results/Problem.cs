namespace Synapsis.Sil.Models.Results;

/// <summary>
/// What went wrong, in the same three parts everywhere in Synapsis: a stable error code, where it is, and a sentence
/// for people. Every component that can fail answers with a <see cref="Result"/> or <see cref="Result{T}"/> that
/// carries a Problem.
/// </summary>
public sealed class Problem
{
    /// <summary>Creates a problem.</summary>
    public Problem(string code, string path, string message)
    {
        Code = code;
        Path = path;
        Message = message;
    }

    /// <summary>The stable error code, one of <see cref="ErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>
    /// Where the problem is, written like "measures[2].value" or "place.logical.path[0]"; empty means the whole input.
    /// </summary>
    public string Path { get; }

    /// <summary>What is wrong, in plain words.</summary>
    public string Message { get; }
}
