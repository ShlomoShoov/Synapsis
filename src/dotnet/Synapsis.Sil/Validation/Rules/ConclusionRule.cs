using System.Collections.Generic;
using Synapsis.Sil.Records;
using Synapsis.Sil.Vocabulary.Terms;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 12 (conclusions): who made it must be in core's list (human, agent, system), and a classification
/// (predicate "in_logical_space") must name a registered space, a path within its depth, and, for a closed space, a
/// registered value. This is rule A-CLOSED: a model can only label things with classes that exist.
/// </summary>
public sealed class ConclusionRule : IRecordRule
{
    /// <summary>The predicate of a classification.</summary>
    private const string Classification = "in_logical_space";

    /// <summary>Returns E-SCHEMA for method.by, E-LOGICAL-SPACE, -DEPTH or -VALUE, or null. Other kinds pass.</summary>
    public RecordError? Check(RecordContext context)
    {
        if (context.Record is not ConclusionRecord conclusion)
        {
            return null;
        }
        if (!context.Registry.MethodBy.Contains(conclusion.Method.By))
        {
            return new RecordError(ErrorCodes.Schema, "method.by",
                "method.by must be one of " + string.Join(", ", context.Registry.MethodBy));
        }
        if (conclusion.Statement.Predicate != Classification)
        {
            return null;
        }
        return CheckClassification(conclusion.Statement.Object ?? string.Empty, context);
    }

    /// <summary>A classification target "space/value/...": the space, the depth, the closed value.</summary>
    private static RecordError? CheckClassification(string target, RecordContext context)
    {
        int slash = target.IndexOf('/');
        string spaceName = slash < 0 ? target : target.Substring(0, slash);
        string rest = slash < 0 ? string.Empty : target.Substring(slash + 1);
        if (!context.Registry.LogicalSpaces.TryGetValue(spaceName, out LogicalSpaceDefinition? space))
        {
            return new RecordError(ErrorCodes.LogicalSpace, "statement.object",
                $"logical space '{spaceName}' is not registered");
        }
        List<string> path = NonEmptyParts(rest);
        if (path.Count == 0 || path.Count > space.MaxDepth)
        {
            return new RecordError(ErrorCodes.LogicalDepth, "statement.object",
                $"'{spaceName}' paths have 1 to {space.MaxDepth} levels");
        }
        if (space.Closed && !space.Values.Contains(path[0]))
        {
            return new RecordError(ErrorCodes.LogicalValue, "statement.object",
                $"'{path[0]}' is not a registered value of '{spaceName}'");
        }
        return null;
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
