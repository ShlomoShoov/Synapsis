using System.Text;

namespace Synapsis.Sil.Services.Schemas;

/// <summary>
/// Writes the location of a problem in the contract's path format: names joined by dots, list positions in brackets,
/// for example "measures[2].value". Rules write their paths directly in this format; this class converts the JSON
/// Pointer locations ("/measures/2/value") that the JSON Schema library reports.
/// </summary>
public static class RecordPath
{
    /// <summary>Converts a JSON Pointer such as "/measures/2/value" into "measures[2].value"; "" stays "".</summary>
    public static string FromJsonPointer(string pointer)
    {
        StringBuilder path = new StringBuilder();
        string[] segments = pointer.Split('/');
        foreach (string rawSegment in segments)
        {
            if (rawSegment.Length == 0)
            {
                continue;
            }
            string segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
            AppendSegment(path, segment);
        }
        return path.ToString();
    }

    /// <summary>Adds one segment: a number becomes [n], a name becomes .name (or just name at the start).</summary>
    private static void AppendSegment(StringBuilder path, string segment)
    {
        if (IsListPosition(segment))
        {
            path.Append('[').Append(segment).Append(']');
            return;
        }
        if (path.Length > 0)
        {
            path.Append('.');
        }
        path.Append(segment);
    }

    /// <summary>True when the segment is a list position (digits only).</summary>
    private static bool IsListPosition(string segment)
    {
        foreach (char character in segment)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }
        return true;
    }
}
