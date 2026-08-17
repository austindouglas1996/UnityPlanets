namespace MarchingTerrain.Systems.Generation
{
    using UnityEngine;

    /// <summary>
    /// GPU-friendly representation of a terrain edit.
    ///
    /// This class contains only world-space data required to apply a density
    /// modification during generation. It intentionally avoids chunk-local or
    /// spatial indexing data, as those are handled on the CPU side.
    ///
    /// Designed to be copied directly into structured buffers and consumed
    /// by compute shaders with minimal processing.
    /// </summary>
    public struct ChunkEditData
    {
        /// <summary>
        /// Creates a new terrain edit centered at a world-space position.
        /// </summary>
        /// <param name="hitPoint">
        /// World-space position where the edit was applied.
        /// </param>
        public ChunkEditData(Vector3 hitPoint, int operation = 0)
            : this()
        {
            this.HitPointWP = hitPoint;
            this.Operation = operation;

            this.Radius = 25f;
            this.Intensity = 15.7f;
        }

        /// <summary>
        /// World-space center of the edit.
        /// All voxel influence is computed relative to this position.
        /// </summary>
        public readonly Vector3 HitPointWP;

        /// <summary>
        /// Radius of influence in world units.
        /// Voxels outside this radius are unaffected.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Strength of the density modification.
        /// Positive or negative depending on add/remove behavior.
        /// </summary>
        public float Intensity;

        /// <summary>
        /// The operation used on the modification.
        /// 0 = Subtracting
        /// 1 = Addition
        /// </summary>
        public int Operation;
    }
}
