using UnityEngine;

[System.Serializable]
public class LODThresholds
{
    [Tooltip("LOD0 — up close: player feet, terrain sculpting, grass")]
    public int LOD0 = 2;

    [Tooltip("LOD1 — near field: trees, paths")]
    public int LOD1 = 4;

    [Tooltip("LOD2 — visible terrain shape, some structure")]
    public int LOD2 = 6;

    [Tooltip("LOD3 — far terrain shape only")]
    public int LOD3 = 8;

    [Tooltip("LOD4 — horizon terrain")]
    public int LOD4 = 16;

    public int[] ToArray() => new[] { LOD0, LOD0+LOD1, LOD1+LOD2, LOD2+LOD3, LOD3+LOD4};
}