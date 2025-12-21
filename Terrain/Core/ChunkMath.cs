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
            new Vector3Int( 1, 0, 0), // +X
            new Vector3Int(-1, 0, 0), // -X
            new Vector3Int( 0, 1, 0), // +Y
            new Vector3Int( 0,-1, 0), // -Y
            new Vector3Int( 0, 0, 1), // +Z
            new Vector3Int( 0, 0,-1), // -Z
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
        /// Returns the chunk size for a given LOD level.
        /// </summary>
        /// <param name="lod"></param>
        /// <returns></returns>
        public int GetChunkSize(int lod)
        {
            return this.Configuration.DensityOptions.CubesPerAxis << lod;
        }

        /// <summary>
        /// Return a set of coordinates to world position.
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public Vector3 ToWorld(Vector3 coordinates)
        {
            int chunkSize = GetChunkSize(0);
            return new Vector3(
                coordinates.x * chunkSize,
                coordinates.y * chunkSize,
                coordinates.z * chunkSize);
        }

        /// <summary>
        /// Return a world position in world coordinates.
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public Vector3Int ToCoordinates(Vector3 worldPositon)
        {
            int chunkSize = GetChunkSize(0);
            return new Vector3Int(
                Mathf.FloorToInt(worldPositon.x / chunkSize),
                Mathf.FloorToInt(worldPositon.y / chunkSize),
                Mathf.FloorToInt(worldPositon.z / chunkSize));
        }

        /// <summary>
        /// Retrieve the <see cref="Bounds"/> for a given <see cref="ChunkKey"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Bounds GetBounds(ChunkKey key)
        {
            return GetBounds(key.Coordinates, key.LODIndex);
        }

        /// <summary>
        /// Retrieve the <see cref="Bounds"/> for a given <see cref="ChunkKey"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Bounds GetBounds(Vector3Int coordinates, int lodIndex)
        {
            int chunkSize = GetChunkSize(lodIndex);

            Vector3 worldPos = new Vector3(
                 coordinates.x * chunkSize,
                 coordinates.y * chunkSize,
                 coordinates.z * chunkSize);

            Bounds bounds = new Bounds
            {
                center = worldPos + new Vector3(chunkSize, chunkSize, chunkSize) * 0.5f,
                size = new Vector3(chunkSize, chunkSize, chunkSize)
            };

            return bounds;
        }

        /// <summary>
        /// Retrieve a set of coordinates based on a <see cref="Bounds"/> object.
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="lodIndex"></param>
        /// <returns></returns>
        public Vector3Int BoundsToCoordinates(Bounds bounds, int lodIndex)
        {
            int chunkSize = GetChunkSize(lodIndex);
            Vector3 pos = bounds.min;

            return new Vector3Int(
                Mathf.FloorToInt(pos.x / chunkSize),
                Mathf.FloorToInt(pos.y / chunkSize),
                Mathf.FloorToInt(pos.z / chunkSize));
        }

        /// <summary>
        /// Retrieve the expected chunk LOD level for a given coordinate.
        /// </summary>
        /// <param name="chunkCoordinates"></param>
        /// <returns></returns>
        public int GetLODForChunk(Vector3Int coord, Vector3 playerWorldPos)
        {
            int chunkSize = GetChunkSize(0);

            Vector3 worldMin = coord * chunkSize;
            Vector3 worldMax = worldMin + new Vector3(chunkSize, chunkSize, chunkSize);

            // Clamp player position to chunk AABB
            float px = Mathf.Clamp(playerWorldPos.x, worldMin.x, worldMax.x);
            float py = Mathf.Clamp(playerWorldPos.y, worldMin.y, worldMax.y);
            float pz = Mathf.Clamp(playerWorldPos.z, worldMin.z, worldMax.z);

            float dx = Mathf.Abs(playerWorldPos.x - px);
            float dy = Mathf.Abs(playerWorldPos.y - py);
            float dz = Mathf.Abs(playerWorldPos.z - pz);

            float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            int ring = Mathf.CeilToInt(dist / chunkSize);

            return DesiredLodFromRings(ring);
        }

        /// <summary>
        /// Builds a 6-bit LOD edge mask for a chunk.
        /// Each bit indicates that the corresponding neighbor is a lower LOD
        /// and therefore requires a Transvoxel transition on that face.
        ///
        /// Bit layout:
        /// 0 = +X, 1 = -X, 2 = +Y, 3 = -Y, 4 = +Z, 5 = -Z
        /// </summary>
        public uint GetLodMaskForChunk(Vector3Int coord, int givenLod, Vector3 playerWorldPos)
        {
            uint mask = 0;

            for (int i = 0; i < 6; i++)
            {
                int neighborLod = GetLODForChunk(coord + ChunkOffsets[i], playerWorldPos);

                // Only generate transitions when this chunk is higher detail
                if (neighborLod < givenLod)
                    mask |= 1u << i;
            }

            return mask;
        }

        /// <summary>
        /// Determine the best LOD ring to use based on the distance.
        /// </summary>
        /// <param name="dChunks0"></param>
        /// <param name="rings"></param>
        /// <returns></returns>
        private int DesiredLodFromRings(int dChunks0)
        {
            for (int i = 0; i < LODRings.Length; i++)
            {
                if (dChunks0 < LODRings[i])
                    return i;
            }

            return LODRings.Length - 1;
        }
    }
}