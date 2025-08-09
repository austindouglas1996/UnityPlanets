using System;
using UnityEngine;

public readonly struct ChunkKey : IEquatable<ChunkKey>
{
    public readonly Vector3Int Coordinates;
    public readonly int LODIndex;

    public ChunkKey(Vector3Int coords, int lod)
    {
        Coordinates = coords;
        LODIndex = lod;
    }

    public bool Equals(ChunkKey other) =>
        Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;

    public override bool Equals(object obj) =>
        obj is ChunkKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Coordinates, LODIndex);

    public override string ToString() =>
        $"Key LOD:{LODIndex} ({Coordinates.x},{Coordinates.y},{Coordinates.z})";
}
