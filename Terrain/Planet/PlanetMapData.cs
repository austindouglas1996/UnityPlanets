using UnityEngine;

public class PlanetMapData
{
    public PlanetMapData(float[,,] densityMap, MeshData data)
    {
        this.DensityMap = densityMap;
        this.MeshData = data;
    }

    public float[,,] DensityMap;
    public MeshData MeshData;
}