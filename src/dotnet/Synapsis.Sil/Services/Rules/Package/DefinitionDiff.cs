using System;
using System.Collections.Generic;

namespace Synapsis.Sil.Services.Rules.Package;

/// <summary>
/// Compares the definitions of two versions of a package: which codes were removed, and each definition by its code.
/// </summary>
public static class DefinitionDiff
{
    /// <summary>Adds "{what} '{item}'{where} was removed" for every item of <paramref name="before"/> missing from <paramref name="after"/>.</summary>
    public static void AddRemoved(string what, string where, IEnumerable<string> before, IEnumerable<string> after, List<string> problems)
    {
        HashSet<string> kept = new HashSet<string>(after);
        foreach (string item in before)
        {
            if (!kept.Contains(item))
            {
                problems.Add($"{what} '{item}'{where} was removed");
            }
        }
    }

    /// <summary>The definitions by their code.</summary>
    public static Dictionary<string, T> Index<T>(IEnumerable<T> definitions, Func<T, string> codeOf)
    {
        Dictionary<string, T> index = new Dictionary<string, T>();
        foreach (T definition in definitions)
        {
            index[codeOf(definition)] = definition;
        }
        return index;
    }
}
