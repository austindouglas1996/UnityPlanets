using UnityEngine;

public class ChunkContext
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

    public override string ToString()
    {
        return $"Chunk LOD:{LODIndex} X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";
    }
}