namespace GingerVoxelSystem.Core
{
    using System;
    using UnityEngine;

    /// <summary>
    /// A lightweight identifier for a chunk in the world.
    /// Combines chunk coordinates with the LOD (level of detail) index,
    /// so we can easily tell one chunk apart from another.
    /// </summary>
    public struct ChunkKey : IEquatable<ChunkKey>
    {
        /// <summary>
        /// Global origin in LOD0 chunk units (authoritative).
        /// This identifies where the chunk exists in world space.
        /// </summary>
        public readonly Vector3Int Origin;

        /// <summary>
        /// Which LOD (Level of Detail) this chunk is at.
        /// Lower = closer / higher detail, higher = farther / less detail.
        /// </summary>
        public readonly int LODIndex;

        /// <summary>
        /// Creates a new key from coordinates and an LOD index.
        /// </summary>
        public ChunkKey(Vector3Int Origin, int lod)
        {
            this.Origin = Origin;
            LODIndex = lod;
        }

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
            Origin.Equals(other.Origin) && LODIndex == other.LODIndex;

        /// <summary>
        /// Equality check against any object (safe cast).
        /// </summary>
        public override bool Equals(object obj) =>
            obj is ChunkKey other && Equals(other);

        /// <summary>
        /// Return the parent of a given child <see cref="ChunkKey"/>.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public ChunkKey GetParent()
        {
            Vector3Int parentOrigin = new Vector3Int(
                this.Origin.x >> 1,
                this.Origin.y >> 1,
                this.Origin.z >> 1
            );

            return new ChunkKey(parentOrigin, this.LODIndex + 1);
        }

        /// <summary>
        /// Gets the center of the key in a LOD0 specification.
        /// </summary>
        public Vector3Int GetWorldCenter(int baseCellStep)
        {
            int span = baseCellStep << LODIndex;

            Vector3Int corner = new Vector3Int(
                Origin.x * span,
                Origin.y * span,
                Origin.z * span
            );

            int half = span >> 1;

            return new Vector3Int(
                corner.x + half,
                corner.y + half,
                corner.z + half
            );
        }

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
                int h = Origin.x * 73856093
                      ^ Origin.y * 19349663
                      ^ Origin.z * 83492791;
                return h ^ (LODIndex * 486187739);
            }
        }

        /// <summary>
        /// Human-readable string version (good for debugging/logs).
        /// </summary>
        public override string ToString() =>
            $"Key LOD:{LODIndex} Origin:{Origin}";
    }
}