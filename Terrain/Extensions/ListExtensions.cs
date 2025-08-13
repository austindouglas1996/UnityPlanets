using System;
using System.Collections.Generic;

public static class ListExtensions
{
    /// <summary>
    /// Removes up to count elements from the front of the list
    /// and returns them in a new list.
    /// </summary>
    public static List<T> PopFront<T>(this List<T> list, int count)
    {
        if (list.Count == 0)
            return new List<T>(0);

        int take = Math.Min(count, list.Count);
        var result = new List<T>(take);

        for (int i = 0; i < take; i++)
            result.Add(list[i]);

        list.RemoveRange(0, take);
        return result;
    }

    /// <summary>
    /// Removes up to count elements from the front of the list
    /// and fills a caller-provided destination list (avoids allocs).
    /// </summary>
    public static void PopFront<T>(this List<T> list, int count, List<T> dst)
    {
        dst.Clear();
        if (list.Count == 0)
            return;

        int take = Math.Min(count, list.Count);
        for (int i = 0; i < take; i++)
            dst.Add(list[i]);

        list.RemoveRange(0, take);
    }
}
