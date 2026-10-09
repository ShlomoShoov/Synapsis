using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 12 (conclusions): who made it must be in core's list (human, agent, system), and a classification
/// (predicate "in_logical_space") must name a registered space, a path within its depth, and, for a closed space, a
/// registered value. This is rule A-CLOSED: a model can only label things with classes that exist.
/// </summary>
public sealed class ConclusionRule : IRecordRule
{
    /// <summary>The predicate of a classification.</summary>
    private const string Classification = "in_logical_space";

    /// <summary>Passes (always for other kinds), or fails with E-SCHEMA for method.by, or E-LOGICAL-SPACE, -DEPTH or -VALUE.</summary>
    public Result Check(RecordContext context)
    {
        if (context.Record is not ConclusionRecord conclusion)
        {
            return Result.Success();
        }
        if (!context.Registry.IsMethodBy(conclusion.Method.By))
        {
            return Result.Failure(ErrorCodes.Schema, "method.by", $"method.by '{conclusion.Method.By}' is not in core's list");
        }
        if (conclusion.Statement.Predicate != Classification)
        {
            return Result.Success();
        }
        return CheckClassification(conclusion.Statement.Object ?? string.Empty, context);
    }

    /// <summary>A classification target "space/value/...": the space, the depth, the closed value.</summary>
    private static Result CheckClassification(string target, RecordContext context)
    {
        int slash = target.IndexOf('/');
        string spaceName = target;
        string rest = string.Empty;
        if (slash >= 0)
        {
            spaceName = target.Substring(0, slash);
            rest = target.Substring(slash + 1);
        }
        LogicalSpaceDefinition? space = context.Registry.FindLogicalSpace(spaceName);
        if (space == null)
        {
            return Result.Failure(ErrorCodes.LogicalSpace, "statement.object", $"logical space '{spaceName}' is not registered");
        }
        List<string> path = NonEmptyParts(rest);
        if (path.Count == 0 || path.Count > space.MaxDepth)
        {
            return Result.Failure(ErrorCodes.LogicalDepth, "statement.object", $"'{spaceName}' paths have 1 to {space.MaxDepth} levels");
        }
        if (space.Closed && !space.Values.Contains(path[0]))
        {
            return Result.Failure(ErrorCodes.LogicalValue, "statement.object", $"'{path[0]}' is not a registered value of '{spaceName}'");
        }
        return Result.Success();
    }

    /// <summary>The parts of "a/b/c" that are not empty.</summary>
    private static List<string> NonEmptyParts(string text)
    {
        List<string> parts = new List<string>();
        foreach (string part in text.Split('/'))
        {
            if (part.Length > 0)
            {
                parts.Add(part);
            }
        }
        return parts;
    }
}
