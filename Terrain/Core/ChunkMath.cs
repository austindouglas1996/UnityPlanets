namespace MarchingTerrain.Core
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
            int baseCellStep = Configuration.DensityOptions.BaseCellStep;
            int span = baseCellStep << key.LODIndex;
            float chunkWorldSize = Configuration.DensityOptions.CellsPerAxis * span;

            // Origin is in LOD-local chunk coords. Convert directly to world space.
            Vector3 worldCorner = new Vector3(key.Origin.x, key.Origin.y, key.Origin.z) * chunkWorldSize;
            Vector3 center = worldCorner + Vector3.one * (chunkWorldSize * 0.5f);

            return new Bounds(center, Vector3.one * (chunkWorldSize * ChunkEngineSettings.EditBoundsInflation));
        }
    }
}