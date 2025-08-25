using UnityEngine;

[System.Serializable]
public class LODThresholds
{
    [Tooltip("LOD0 — up close: player feet, terrain sculpting, grass")]
    public int LOD0 = 2;

    [Tooltip("LOD1 — near field: trees, paths")]
    public int LOD1 = 2;

    [Tooltip("LOD2 — visible terrain shape, some structure")]
    public int LOD2 = 2;

    [Tooltip("LOD3 — far terrain shape only")]
    public int LOD3 = 2;

    [Tooltip("LOD4 — horizon terrain")]
    public int LOD4 = 2;

    public int[] ToArray()
    {
        int[] counts = { LOD0, LOD1, LOD2, LOD3, LOD4 };
        int[] rings = new int[counts.Length];
        int acc = 0;

        for (int L = 0; L < counts.Length; L++)
        {
            int step = 1 << L;
            acc += counts[L] * step;
            rings[L] = acc;
        }

        return rings;
    }
}