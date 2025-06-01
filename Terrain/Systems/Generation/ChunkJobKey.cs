using System;
using UnityEngine;

public readonly struct ChunkJobKey : IEquatable<ChunkJobKey>
{
    public readonly Vector3Int Coordinates;
    public readonly int LODIndex;

    public ChunkJobKey(Vector3Int coordinates, int lodIndex)
    {
        Coordinates = coordinates;
        LODIndex = lodIndex;
    }

    public bool Equals(ChunkJobKey other)
        => Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;

    public override int GetHashCode()
        => HashCode.Combine(Coordinates, LODIndex);

    public override bool Equals(object obj)
        => obj is ChunkJobKey other && Equals(other);
}