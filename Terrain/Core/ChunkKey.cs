namespace GingerVoxelSystem.Core
{
    using System;
    using UnityEngine;

    /// <summary>
    /// A lightweight identifier for a chunk in the world.
    /// Combines chunk coordinates with the LOD (level of detail) index,
    /// so we can easily tell one chunk apart from another.
    /// </summary>
    public readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        /// <summary>
        /// A universal ID based on this chunk's position in LOD0-space.
        /// Useful for spatial alignment across LODs.
        /// </summary>
        public Vector3Int Global => Coordinates * (1 << LODIndex);

        /// <summary>
        /// The 3D grid coordinates of the chunk.
        /// </summary>
        public readonly Vector3Int Coordinates;

        /// <summary>
        /// Which LOD (Level of Detail) this chunk is at.
        /// Lower = closer / higher detail, higher = farther / less detail.
        /// </summary>
        public readonly int LODIndex;

        /// <summary>
        /// Creates a new key from coordinates and an LOD index.
        /// </summary>
        public ChunkKey(Vector3Int coords, int lod)
        {
            Coordinates = coords;
            LODIndex = lod;
        }

        /// <summary>
        /// How many LOD0 chunks this key contains.
        /// </summary>
        public int Size0 => 1 << LODIndex;

        public static bool operator ==(ChunkKey a, ChunkKey b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ChunkKey a, ChunkKey b)
        {
            return !a.Equals(b);
        }

        /// <summary>
        /// An invalid key reference.
        /// </summary>
        public static readonly ChunkKey Invalid = new ChunkKey(new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), -1);

        /// <summary>
        /// Checks if another ChunkKey matches this one
        /// (same coordinates AND same LOD).
        /// </summary>
        public bool Equals(ChunkKey other) =>
            Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;

        /// <summary>
        /// Equality check against any object (safe cast).
        /// </summary>
        public override bool Equals(object obj) =>
            obj is ChunkKey other && Equals(other);

        /// <summary>
        /// Generates a unique hash so this key works in dictionaries/sets.
        /// </summary>
        /// <remarks>While trying to resolve memory issues in <see cref="ChunkRenderBucket"/> with large item amounts
        /// 400k elements. I found this https://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-overriding-gethashcode/263416#263416
        /// it made no change.</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = Coordinates.x * 73856093
                      ^ Coordinates.y * 19349663
                      ^ Coordinates.z * 83492791;
                return (h ^ (LODIndex * 486187739));
            }
        }

        /// <summary>
        /// Human-readable string version (good for debugging/logs).
        /// </summary>
        public override string ToString() =>
            $"Key LOD:{LODIndex} ({Coordinates.x},{Coordinates.y},{Coordinates.z})";
    }
}