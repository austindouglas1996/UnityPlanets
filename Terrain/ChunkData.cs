using UnityEngine;

public class ChunkData
{
    public ChunkData(float[,,] densityMap, MeshData data)
    {
        this.DensityMap = densityMap;
        this.MeshData = data;
    }

    public float[,,] DensityMap;
    public MeshData MeshData;
}