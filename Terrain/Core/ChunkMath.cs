namespace GingerVoxelSystem.Core
{
    using UnityEngine;

    /// <summary>
    /// Provides stateless helper functions for chunk coordinate math,
    /// size calculations, bounds generation, and distance-based LOD selection.
    /// This class contains no runtime state; it operates entirely on the
    /// configuration supplied during construction.
    /// </summary>
    public class ChunkMath
    {
        public static readonly Vector3Int[] ChunkOffsets =
        {
            new Vector3Int(-1, 0, 0), // -X
            new Vector3Int( 1, 0, 0), // +X
            new Vector3Int( 0,-1, 0), // -Y
            new Vector3Int( 0, 1, 0), // +Y
            new Vector3Int( 0, 0,-1), // -Z
            new Vector3Int( 0, 0, 1), // +Z
        };

        /// <summary>
        /// Configuration data for how to handle different math functions.
        /// </summary>
        private IChunkConfiguration Configuration;

        /// <summary>
        /// Initializes a new instance of <see cref="ChunkMath"/>
        /// </summary>
        /// <param name="configuration"></param>
        public ChunkMath(IChunkConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Retrieve the bounds for a given chunk.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Bounds GetBounds(ChunkKey key)
        {
            int span = 1 << key.LODIndex;
            float chunkSize = Configuration.DensityOptions.CellsPerAxis * span;

            // Convert LOD0 chunk coordinates to world-space origin
            Vector3 worldOrigin = key.BaseCenter * Configuration.DensityOptions.CellsPerAxis;

            // Center is origin + half size
            Vector3 center = worldOrigin + Vector3.one * (chunkSize * 0.5f);

            // The size of this can be modified to create false positives
            // this will elimate issues with things not rendering correctly.
            return new Bounds(center, Vector3.one * (chunkSize * ChunkEngineSettings.EditBoundsInflation));
        }


        /// <summary>
        /// Converts a world-space position into the origin position of the
        /// corresponding LOD0 chunk.
        /// 
        /// The returned position represents the chunk's base (min corner)
        /// in world space.
        /// </summary>
        public Vector3Int WorldToChunkOriginLOD0(Vector3 worldPos)
        {
            int chunkSize = Configuration.DensityOptions.CellsPerAxis;

            int x = Mathf.FloorToInt(worldPos.x / chunkSize);
            int y = Mathf.FloorToInt(worldPos.y / chunkSize);
            int z = Mathf.FloorToInt(worldPos.z / chunkSize);

            return new Vector3Int(x, y, z);
        }
    }
}