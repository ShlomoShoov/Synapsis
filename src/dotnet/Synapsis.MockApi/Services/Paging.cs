using System.Collections.Generic;
using System.Globalization;

namespace Synapsis.MockApi.Services;

/// <summary>Pages of a list: the cursor is the position of the page's first item (the mock's own; the UI only sends back what it got).</summary>
public static class Paging
{
    /// <summary>At most limit items from the cursor (none or unreadable: the start); next is the following position, or null at the end.</summary>
    public static List<T> Page<T>(List<T> all, string? cursor, int limit, out string? next)
    {
        int start = 0;
        if (cursor != null && int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out int position) && position < all.Count)
        {
            start = position;
        }
        int count = limit < all.Count - start ? limit : all.Count - start;
        next = start + count < all.Count ? (start + count).ToString(CultureInfo.InvariantCulture) : null;
        return all.GetRange(start, count);
    }
}
