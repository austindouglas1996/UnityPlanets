namespace MarchingTerrain.EditorSupport
{
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
        public int LOD1 = 4;

        [Tooltip("LOD2 — visible terrain shape, some structure")]
        public int LOD2 = 8;

        [Tooltip("LOD3 — far terrain shape only")]
        public int LOD3 = 16;

        [Tooltip("LOD4 — horizon terrain")]
        public int LOD4 = 32;

        [Tooltip("LOD5 — far horizon terrain")]
        public int LOD5 = 64;

        [Tooltip("LOD5 — very far horizon terrain")]
        public int LOD6 = 128;

        /// <summary>
        /// Build an array of accumulated ring distances based on LOD0.
        /// Example: with all counts = 2, returns [2, 6, 14, 30, 62].
        /// </summary>
        public int[] ToArray()
        {
            return new int[] { LOD0, LOD1, LOD2, LOD3, LOD4, LOD5, LOD6 };
        }
    }
}