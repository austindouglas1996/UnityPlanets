using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ListHelper 
{
    public static T Random<T>(this List<T> list)
    {
        if (list.Count == 0)
        {
            Debug.LogWarning("list is empty.");
            return default(T);
        }

        int randomIndex = UnityEngine.Random.Range(0, list.Count);
        return list[randomIndex];
    }
}
