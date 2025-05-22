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
    public ChunkData(DensityMap densityMap, MeshData data, ChunkContext context)
    {
        this.DensityMap = densityMap;
        this.MeshData = data;
        Context = context;
    }

    public ChunkContext Context;
    public DensityMap DensityMap;
    public MeshData MeshData;

    public bool IsRenderable => MeshData != null && !MeshData.IsEmpty;

    public Mesh GenerateMesh()
    {
        return this.MeshData.GenerateMesh(this.DensityMap);
    }
}