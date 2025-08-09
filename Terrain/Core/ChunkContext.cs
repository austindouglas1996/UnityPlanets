using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the context of a specific chunk in the world.
/// Holds its coordinates, LOD level, and shared service references.
/// </summary>
public class ChunkContext : IEquatable<ChunkContext>
{
    /// <summary>
    /// Initializes a new ChunkContext with position, LOD, and services.
    /// </summary>
    public ChunkContext(Vector3Int coordinates, int lODIndex, IChunkServices services)
    {
        Coordinates = coordinates;
        LODIndex = lODIndex;
        Services = services;

        WorldPosition = Services.Layout.ToWorld(Coordinates, LODIndex);
        Transform = Matrix4x4.TRS(this.WorldPosition, Quaternion.identity, Vector3.one);
    }

    /// <summary>
    /// The grid coordinate of this chunk (in chunk units, not world units).
    /// </summary>
    public Vector3Int Coordinates { get; }

    /// <summary>
    /// The Level of Detail index. LOD0 is highest detail.
    /// </summary>
    public int LODIndex { get; }

    /// <summary>
    /// Shared services passed to the chunk (layout, config, generation).
    /// </summary>
    public IChunkServices Services { get; }

    /// <summary>
    /// World position of the chunk, derived from layout and coordinates.
    /// </summary>
    public Vector3 WorldPosition { get; private set; }

    /// <summary>
    /// Creates a transformation matrix.
    /// </summary>
    public Matrix4x4 Transform { get; }

    /// <summary>
    /// Standard object.Equals override for comparing context.
    /// </summary>
    public override bool Equals(object obj) => Equals(obj as ChunkContext);

    /// <summary>
    /// Compares two contexts based on position and LOD.
    /// </summary>
    public bool Equals(ChunkContext other)
    {
        if (other is null)
            return false;

        return Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;
    }

    /// <summary>
    /// HashCode override — uses coordinate and LOD for dictionary use.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Coordinates.GetHashCode();
            hash = hash * 31 + LODIndex.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// Debug string — shows LOD and coordinates for logging.
    /// </summary>
    public override string ToString()
    {
        return $"Chunk LOD:{LODIndex} X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";
    }
}

/// <summary>
/// A simple comparer.
/// </summary>
public class ChunkContextComparer : IEqualityComparer<ChunkContext>
{
    public bool Equals(ChunkContext x, ChunkContext y)
    {
        if (x == null || y == null)
            return false;

        return x.Coordinates == y.Coordinates &&
               x.LODIndex == y.LODIndex;
    }

    public int GetHashCode(ChunkContext obj)
    {
        return HashCode.Combine(obj.Coordinates, obj.LODIndex);
    }
}
