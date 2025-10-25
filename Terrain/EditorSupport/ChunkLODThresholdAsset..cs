using UnityEngine;

/// <summary>
/// LOD ring settings for chunk generation.
/// Each LOD has a count value (how many "rings" of chunks it spans).
/// <see cref="ToArray"/> converts them into accumulated ring distances.
/// </summary>
[CreateAssetMenu(menuName = "Terrain/Level Of Detail (LOD)", fileName = "LODLevel")]
public class ChunkLODThresholdAsset : ScriptableObject
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

    [Tooltip("LOD5 — far horizon terrain")]
    public int LOD5 = 2;

    [Tooltip("LOD5 — very far horizon terrain")]
    public int LOD6 = 2;

    /// <summary>
    /// Build an array of accumulated ring distances based on LOD0.
    /// Example: with all counts = 2, returns [2, 6, 14, 30, 62].
    /// </summary>
    public int[] ToArray()
    {
        int[] counts = { LOD0, LOD1, LOD2, LOD3, LOD4, LOD5, LOD6};
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