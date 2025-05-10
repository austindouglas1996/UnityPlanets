using UnityEngine;

/// <summary>
/// Contains the data to make, or maintain a chunk in the marching cube implementation.
/// </summary>
public class ChunkData
{
    /// <summary>
    /// Creates a new chunk data container with the provided voxel density map and mesh data.
    /// Used to pass chunk info through generation, modification, and rendering stages.
    /// </summary>
    /// <param name="densityMap">3D array representing voxel densities for terrain generation.</param>
    /// <param name="data">Initial mesh data generated from the density map.</param>
    public ChunkData(float[,,] densityMap, float[,] surfaceMap, float[,,] foliageMask, MeshData data)
    {
        this.DensityMap = densityMap;
        this.SurfaceMap = surfaceMap;
        this.MeshData = data;
        this.FoliageMask = foliageMask;
    }

    public float[,,] DensityMap;
    public float[,] SurfaceMap;
    public float[,,] FoliageMask;
    public MeshData MeshData;

    public bool IsRenderable => MeshData != null && !MeshData.IsEmpty;
}