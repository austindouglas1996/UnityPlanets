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
        /// Global origin in LOD0 chunk units (authoritative).
        /// This identifies where the chunk exists in world space.
        /// </summary>
        public readonly Vector3Int Origin0;

        /// <summary>
        /// Which LOD (Level of Detail) this chunk is at.
        /// Lower = closer / higher detail, higher = farther / less detail.
        /// </summary>
        public readonly int LODIndex;

        /// <summary>
        /// Creates a new key from coordinates and an LOD index.
        /// </summary>
        public ChunkKey(Vector3Int origin0, int lod)
        {
            this.Origin0 = origin0;
            LODIndex = lod;
        }

        /// <summary>
        /// Coordinates of this chunk in its own LOD grid.
        /// This is DERIVED and should not be used for neighbor queries.
        /// </summary>
        public Vector3Int Coordinates => new Vector3Int(Origin0.x >> LODIndex, Origin0.y >> LODIndex, Origin0.z >> LODIndex);

        /// <summary>
        /// How many LOD0 chunks this key contains.
        /// </summary>
        public int Size0 => 1 << LODIndex;

        public static bool operator ==(ChunkKey a, ChunkKey b) => a.Equals(b);
        public static bool operator !=(ChunkKey a, ChunkKey b) => !a.Equals(b);

        /// <summary>
        /// An invalid key reference.
        /// </summary>
        public static readonly ChunkKey Invalid = new ChunkKey(new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), -1);

        /// <summary>
        /// Checks if another ChunkKey matches this one
        /// (same coordinates AND same LOD).
        /// </summary>
        public bool Equals(ChunkKey other) =>
            Origin0.Equals(other.Origin0) && LODIndex == other.LODIndex;

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
                int h = Origin0.x * 73856093
                      ^ Origin0.y * 19349663
                      ^ Origin0.z * 83492791;
                return h ^ (LODIndex * 486187739);
            }
        }

        /// <summary>
        /// Human-readable string version (good for debugging/logs).
        /// </summary>
        public override string ToString() =>
            $"Key LOD:{LODIndex} Origin0:{Origin0}";
    }
}