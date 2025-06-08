using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct Triangle
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public Color colorA;
    public Color colorB;
    public Color colorC;

    public Vector3 normal;

    public Vector2 UVA;
    public Vector2 UVB;
    public Vector2 UVC;
}

struct BiomeData
{
    public float MinSurface;
    public float MaxSurface;
    public Vector4 GradientStart;
    public Vector4 GradientEnd;
}

/// <summary>
/// Base class for implementing marching cube terrain generation.
/// Handles mesh generation, density map modification, and interpolation.
/// </summary>
public class BaseMarchingCubeGenerator : IDensityMapGenerator
{
    private IChunkConfiguration configuration;
    private System.Random random = new System.Random();

    /// <summary>
    /// Creates a new marching cube generator with the given density options.
    /// </summary>
    /// <param name="options">The configuration used for density generation and surface thresholds.</param>
    public BaseMarchingCubeGenerator(IChunkConfiguration configuration, DensityMapOptions options)
    {
        this.configuration = configuration;
        this.Options = options;

        this.InitBuffer();
    }

    /// <summary>
    /// The options used for generating density and controlling surface behavior.
    /// </summary>
    public DensityMapOptions Options { get; set; }

    private ComputeBuffer BiomeBuffer;
    private int BiomeCount = 0;

    private void InitBuffer()
    {
        var biomes = configuration.Biomes.OrderBy(b => b.MinSurface).ToList();
        BiomeCount = biomes.Count;

        var biomeData = new BiomeData[biomes.Count];
        for (int i = 0; i < biomes.Count; i++)
        {
            biomeData[i] = new BiomeData
            {
                MinSurface = biomes[i].MinSurface,
                MaxSurface = biomes[i].MaxSurface,
                GradientStart = biomes[i].SurfaceColorRange.colorKeys[0].color,
                GradientEnd = biomes[i].SurfaceColorRange.colorKeys[^1].color,
            };
        }

        BiomeBuffer = new ComputeBuffer(biomeData.Length, Marshal.SizeOf<BiomeData>());
        BiomeBuffer.SetData(biomeData);
    }

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
        ComputeBuffer triangleBuffer = new ComputeBuffer(voxelCount * 5, Marshal.SizeOf<Triangle>(), ComputeBufferType.Append);
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer.SetCounterValue(1);

        genShader.SetBuffer(genKernel, "DensityMap", densityBuffer);


        genShader.Dispatch(genKernel, Mathf.CeilToInt(size / 8f), Mathf.CeilToInt(size / 8f), Mathf.CeilToInt(size / 8f));

        // === Dispatch Marching Cubes ===
        ComputeShader mcShader = context.Services.ChunkManager.MarchingCubes;
        int mcKernel = mcShader.FindKernel("March");

        mcShader.SetBuffer(mcKernel, "DensityMap", densityBuffer);
        mcShader.SetBuffer(mcKernel, "TriangleBuffer", triangleBuffer);


        mcShader.SetBuffer(mcKernel, "BiomeColors", BiomeBuffer);
        mcShader.SetInt("_BiomeCount", BiomeCount);

        mcShader.SetInt("_SizeX", size);
        mcShader.SetInt("_SizeY", size);
        mcShader.SetInt("_SizeZ", size);
        mcShader.SetFloat("_BaseX", baseX);
        mcShader.SetFloat("_BaseY", baseY);
        mcShader.SetFloat("_BaseZ", baseZ);
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
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

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

            colors.Add(t.colorA);
            colors.Add(t.colorB);
            colors.Add(t.colorC);

            normals.Add(t.normal);
            normals.Add(t.normal);
            normals.Add(t.normal);

            //uvs.Add(t.UVA);
            //uvs.Add(t.UVB);
            //uvs.Add(t.UVC);
        }

        // Cleanup
        densityBuffer.Dispose();
        triangleBuffer.Dispose();
        countBuffer.Dispose();

        // Package mesh
        MeshData data = new MeshData(vertices, indices, normals, uvs);
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
}