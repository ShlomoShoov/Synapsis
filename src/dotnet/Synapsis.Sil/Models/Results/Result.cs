namespace Synapsis.Sil.Models.Results;

/// <summary>
/// The answer of a check: it passed, or it failed with a <see cref="Problem"/>. Every check in Synapsis answers this
/// way, so every check is called and read the same way.
/// </summary>
public sealed class Result
{
    /// <summary>The one passing result (it carries nothing).</summary>
    private static readonly Result Passed = new Result(null);

    /// <summary>Creates a result; null means passed.</summary>
    private Result(Problem? problem)
    {
        Problem = problem;
    }

    /// <summary>True when the check passed.</summary>
    public bool Succeeded
    {
        get { return Problem == null; }
    }

    /// <summary>What went wrong, when the check failed; null when it passed.</summary>
    public Problem? Problem { get; }

    /// <summary>A passing result.</summary>
    public static Result Success()
    {
        return Passed;
    }

    /// <summary>A failing result with this problem.</summary>
    public static Result Failure(Problem problem)
    {
        return new Result(problem);
    }

    /// <summary>A failing result, built from the problem's three parts.</summary>
    public static Result Failure(string code, string path, string message)
    {
        return new Result(new Problem(code, path, message));
    }
}
