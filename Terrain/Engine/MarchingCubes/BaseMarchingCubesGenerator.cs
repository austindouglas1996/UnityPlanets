using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct Triangle
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;
}


/// <summary>
/// Base class for implementing marching cube terrain generation.
/// Handles mesh generation, density map modification, and interpolation.
/// </summary>
public abstract class BaseMarchingCubeGenerator : IDensityMapGenerator
{
    /// <summary>
    /// Used to help with vertex coloring.
    /// </summary>
    private IChunkColorizer _colorizer;

    private System.Random random = new System.Random();

    /// <summary>
    /// Creates a new marching cube generator with the given density options.
    /// </summary>
    /// <param name="options">The configuration used for density generation and surface thresholds.</param>
    public BaseMarchingCubeGenerator(IChunkColorizer colorizer, DensityMapOptions options)
    {
        if (colorizer == null)
            throw new ArgumentNullException("colorizer is null.");
        if (options == null)
            throw new System.ArgumentNullException("options is null.");

        this._colorizer = colorizer;
        this.Options = options;
    }

    /// <summary>
    /// The options used for generating density and controlling surface behavior.
    /// </summary>
    public DensityMapOptions Options { get; set; }

    public virtual MeshData GenerateMeshData(ChunkContext context)
    {
        Vector3 chunkWorldPosition = context.WorldPosition;
        int lodIndex = context.LODIndex;

        int stepSize = 1 << lodIndex;
        int size = Options.ChunkSize + 1;
        int voxelCount = size * size * size;

        // === Dispatch GenerateDensity ===
        ComputeShader genShader = context.Services.ChunkManager.GenerateDensity;
        int genKernel = genShader.FindKernel("Generate");

        Vector3Int chunkCoord = new Vector3Int(
            Mathf.FloorToInt(chunkWorldPosition.x / (Options.ChunkSize * stepSize)),
            Mathf.FloorToInt(chunkWorldPosition.y / (Options.ChunkSize * stepSize)),
            Mathf.FloorToInt(chunkWorldPosition.z / (Options.ChunkSize * stepSize))
        );

        int baseX = chunkCoord.x * Options.ChunkSize * stepSize;
        int baseY = chunkCoord.y * Options.ChunkSize * stepSize;
        int baseZ = chunkCoord.z * Options.ChunkSize * stepSize;
        int chunkIndex = HashChunkCoordinates(context.Coordinates);

        genShader.SetInt("_SizeX", size);
        genShader.SetInt("_SizeY", size);
        genShader.SetInt("_SizeZ", size);
        genShader.SetInt("_StepSize", stepSize);
        genShader.SetFloat("_Seed", Options.Seed);
        genShader.SetFloat("_BaseX", baseX);
        genShader.SetFloat("_BaseY", baseY);
        genShader.SetFloat("_BaseZ", baseZ);
        genShader.SetFloat("_ChunkIndex", chunkIndex);
        genShader.SetFloat("_ISOLevel", Options.ISOLevel);
        genShader.SetFloat("_ContinentFrequency", Options.ContinentFrequency);
        genShader.SetFloat("_ContinentAmplitude", Options.ContinentAmplitude);
        genShader.SetFloat("_DetailFrequency", Options.DetailFrequency);
        genShader.SetFloat("_DetailAmplitude", Options.DetailAmplitude);
        genShader.SetFloat("_FlatnessFrequency", Options.FlatnessFrequency);
        genShader.SetFloat("_FlatnessStrength", Options.FlatnessStrength);
        genShader.SetFloat("_MountainFrequency", Options.MountainFrequency);
        genShader.SetFloat("_MountainAmplitude", Options.MountainAmplitude);
        genShader.SetFloat("_MountainSharpness", Options.MountainSharpness);
        genShader.SetFloat("_TotalHeightScale", Options.TotalHeightScale);

        // === Buffers ===
        ComputeBuffer densityBuffer = new ComputeBuffer(voxelCount, sizeof(float));
        ComputeBuffer triangleBuffer = new ComputeBuffer(voxelCount * 5, sizeof(float) * 9, ComputeBufferType.Append);
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer.SetCounterValue(1);

        genShader.SetBuffer(genKernel, "DensityMap", densityBuffer);


        genShader.Dispatch(genKernel, Mathf.CeilToInt(size / 8f), Mathf.CeilToInt(size / 8f), Mathf.CeilToInt(size / 8f));

        // === Dispatch Marching Cubes ===
        ComputeShader mcShader = context.Services.ChunkManager.MarchingCubes;
        int mcKernel = mcShader.FindKernel("March");

        mcShader.SetBuffer(mcKernel, "DensityMap", densityBuffer);
        mcShader.SetBuffer(mcKernel, "TriangleBuffer", triangleBuffer);

        mcShader.SetInt("_SizeX", size);
        mcShader.SetInt("_SizeY", size);
        mcShader.SetInt("_SizeZ", size);
        mcShader.SetInt("_StepSize", stepSize);
        mcShader.SetFloat("_IsoLevel", Options.ISOLevel);

        mcShader.Dispatch(mcKernel, Mathf.CeilToInt(size / 4f), Mathf.CeilToInt(size / 4f), Mathf.CeilToInt(size / 4f));

        // === Read triangle count and data ===
        ComputeBuffer.CopyCount(triangleBuffer, countBuffer, 0);
        int[] triCountArr = new int[1];
        countBuffer.GetData(triCountArr);
        int triCount = triCountArr[0];

        Triangle[] rawTris = new Triangle[triCount];
        triangleBuffer.GetData(rawTris);

        // === Convert triangles to mesh ===
        List<Vector3> vertices = new List<Vector3>(triCount * 3);
        List<int> indices = new List<int>(triCount * 3);
        List<Color32> colors = new List<Color32>(triCount * 3);

        for (int i = 0; i < triCount; i++)
        {
            Triangle t = rawTris[i];
            int baseIndex = vertices.Count;

            vertices.Add(t.a);
            vertices.Add(t.b);
            vertices.Add(t.c);

            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);

            colors.Add(_colorizer.GetColorForVertice(t.a + chunkWorldPosition));
            colors.Add(_colorizer.GetColorForVertice(t.b + chunkWorldPosition));
            colors.Add(_colorizer.GetColorForVertice(t.c + chunkWorldPosition));
        }

        // Cleanup
        densityBuffer.Dispose();
        triangleBuffer.Dispose();
        countBuffer.Dispose();

        // Package mesh
        MeshData data = new MeshData(vertices, indices, new List<Vector2>());
        data.Colors = colors.ToArray();
        return data;
    }

    private int HashChunkCoordinates(Vector3Int coord)
    {
        unchecked // allow overflow
        {
            int hash = 17;
            hash = hash * 31 + coord.x;
            hash = hash * 31 + coord.y;
            hash = hash * 31 + coord.z;
            return hash;
        }
    }

    /// <summary>
    /// Modifies the density map in place using a terrain brush.
    /// Adds or subtracts density values based on brush settings and hit point.
    /// </summary>
    /// <param name="brush">The brush to apply to the chunk.</param>
    /// <param name="densityMap">The density map to modify.</param>
    /// <param name="chunkPos">Chunk position in chunk space.</param>
    /// <param name="hitPoint">World-space location the brush is applied to.</param>
    /// <param name="isAdding">If true, adds density; otherwise subtracts.</param>
    public virtual void ModifyMapWithBrush(TerrainBrush brush, ref ScalerField3 densityMap, Vector3Int chunkPos, bool isAdding)
    {
        int width = densityMap.SizeX - 1;
        int height = densityMap.SizeY - 1;
        int depth = densityMap.SizeZ - 1;

        Vector3 chunkWorldOrigin = new Vector3(
            chunkPos.x * width,
            chunkPos.y * height,
            chunkPos.z * depth);

        Vector3 localMin = brush.Min - chunkWorldOrigin;
        Vector3 localMax = brush.Max - chunkWorldOrigin;

        // Clamp to valid voxel range
        int minX = Mathf.Max(0, Mathf.FloorToInt(localMin.x));
        int minY = Mathf.Max(0, Mathf.FloorToInt(localMin.y));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(localMin.z));

        int maxX = Mathf.Min(width, Mathf.CeilToInt(localMax.x));
        int maxY = Mathf.Min(height, Mathf.CeilToInt(localMax.y));
        int maxZ = Mathf.Min(depth, Mathf.CeilToInt(localMax.z));

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3 voxelWorldPos = chunkWorldOrigin + new Vector3(x, y, z);
                    float effect = brush.GetEffectAmount(voxelWorldPos, brush.WorldHitPoint);

                    if (effect == 0) continue;

                    if (isAdding)
                        densityMap[x, y, z] += effect;
                    else
                        densityMap[x, y, z] -= effect;

                    densityMap[x, y, z] = Mathf.Clamp(densityMap[x, y, z], 0f, 1f);
                }
            }
        }
    }
}