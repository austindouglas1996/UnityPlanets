using System;
using UnityEngine;

/// <summary>
/// Identifies a chunk region in the world using region grid coordinates.
/// Useful for grouping multiple chunks into a single batch for processing/rendering.
/// </summary>
public struct ChunkRegionKey : IEquatable<ChunkRegionKey>
{
    public Vector3Int Coordinates { get; }

    public ChunkRegionKey(Vector3Int coordinates)
    {
        Coordinates = coordinates;
    }

    public bool Equals(ChunkRegionKey other)
    {
        return Coordinates.Equals(other.Coordinates);
    }

    public override bool Equals(object obj)
    {
        return obj is ChunkRegionKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Coordinates.GetHashCode();
    }

    public static bool operator ==(ChunkRegionKey left, ChunkRegionKey right) => left.Equals(right);
    public static bool operator !=(ChunkRegionKey left, ChunkRegionKey right) => !left.Equals(right);

    public override string ToString()
    {
        return $"Region X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";
    }
}
