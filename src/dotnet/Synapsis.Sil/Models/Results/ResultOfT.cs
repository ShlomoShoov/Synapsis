namespace Synapsis.Sil.Models.Results;

/// <summary>
/// The answer of an operation that produces something (reading a record, building the registry): the value, or a
/// <see cref="Problem"/> saying why there is none.
/// </summary>
/// <typeparam name="T">The kind of value produced.</typeparam>
public sealed class Result<T>
{
    /// <summary>Creates a result: a value, or a problem.</summary>
    private Result(T? value, Problem? problem)
    {
        Value = value;
        Problem = problem;
    }

    /// <summary>True when the value was produced.</summary>
    public bool Succeeded
    {
        get { return Problem == null; }
    }

    /// <summary>The value, when the operation succeeded.</summary>
    public T? Value { get; }

    /// <summary>What went wrong, when it failed; null when it succeeded.</summary>
    public Problem? Problem { get; }

    /// <summary>A successful result with this value.</summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value, null);
    }

    /// <summary>A failing result with this problem.</summary>
    public static Result<T> Failure(Problem problem)
    {
        return new Result<T>(default, problem);
    }

    /// <summary>A failing result, built from the problem's three parts.</summary>
    public static Result<T> Failure(string code, string path, string message)
    {
        return new Result<T>(default, new Problem(code, path, message));
    }
}
