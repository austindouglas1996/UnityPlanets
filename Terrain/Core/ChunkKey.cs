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
    public override int GetHashCode() =>
        HashCode.Combine(Coordinates, LODIndex);

    /// <summary>
    /// Human-readable string version (good for debugging/logs).
    /// </summary>
    public override string ToString() =>
        $"Key LOD:{LODIndex} ({Coordinates.x},{Coordinates.y},{Coordinates.z})";
}
