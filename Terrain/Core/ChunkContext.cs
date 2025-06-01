using System;
using UnityEngine;

public class ChunkContext : IEquatable<ChunkContext>
{
    public ChunkContext(Vector3Int coordinates, int lODIndex, IChunkServices services)
    {
        Coordinates = coordinates;
        LODIndex = lODIndex;
        Services = services;
    }
    
    public Vector3Int Coordinates { get; }
    public int LODIndex { get; }
    public IChunkServices Services { get; }


    public Vector3 WorldPosition => Services.Layout.ToWorld(Coordinates, LODIndex);

    public override bool Equals(object obj) => Equals(obj as ChunkContext);

    public bool Equals(ChunkContext other)
    {
        if (other is null)
            return false;

        return Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;
    }

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

    public override string ToString()
    {
        return $"Chunk LOD:{LODIndex} X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";
    }
}