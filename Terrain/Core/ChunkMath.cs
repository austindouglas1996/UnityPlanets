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
        /// The set of LOD thresholds for chunk rendering.
        /// </summary>
        private int[] LODRings;

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
            this.LODRings = this.Configuration.LODThresholds.ToArray();
        }

        /// <summary>
        /// Retrieve the expected chunk LOD level for a given coordinate.
        /// </summary>
        /// <param name="chunkCoordinates"></param>
        /// <returns></returns>
        public int GetLODForChunk(Vector3Int chunkOrigin, Vector3 playerWorldPos)
        {
            // Convert player position into LOD0 chunk coordinates
            int playerChunkX = Mathf.FloorToInt(playerWorldPos.x / 16);
            int playerChunkZ = Mathf.FloorToInt(playerWorldPos.z / 16);

            int dx = Mathf.Abs(chunkOrigin.x - playerChunkX);
            int dz = Mathf.Abs(chunkOrigin.z - playerChunkZ);

            int ring = Mathf.Max(dx, dz);

            for (int i = 0; i < LODRings.Length; i++)
            {
                if (ring < LODRings[i])
                    return i;
            }

            return LODRings.Length - 1;
        }

        /// <summary>
        /// Computes the LOD edge mask for a chunk, indicating which faces border
        /// neighboring chunks of a higher-detail LOD (lower LOD index).
        ///
        /// A bit is set for each face where the adjacent region is represented
        /// by a chunk with a lower LOD index, meaning the neighbor is more detailed
        /// and requires LOD transition stitching on that face.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="position"></param>
        /// <returns>A 6-bit mask where each bit corresponds to a cube face that borders a
        /// higher-detail neighboring chunk.</returns>
        public uint GetLODEdgeMask(ChunkKey key, Vector3 position)
        {
            if (key.LODIndex == 0)
                return 0;

            uint mask = 0;

            Vector3Int origin0 = key.BaseCenter;
            int span = 1 << key.LODIndex; // how many LOD0 chunks this chunk spans

            for (int face = 0; face < 6; face++)
            {
                Vector3Int offset = ChunkMath.ChunkOffsets[face];

                Vector3Int neighborOrigin0 = origin0 + new Vector3Int(
                    offset.x * span,
                    offset.y * span,
                    offset.z * span
                );

                if (GetLODForChunk(neighborOrigin0, position) < key.LODIndex)
                    mask |= 1u << face;
            }

            return mask;
        }
    }
}