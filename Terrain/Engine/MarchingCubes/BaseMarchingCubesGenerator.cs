using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkInput
{
    public Vector3 CoordPos;
    public Vector3 WorldPos;
    public int stepSize;
}

[StructLayout(LayoutKind.Sequential)]
public struct Triangle
{
    public Vector3 CoordPos;

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

public class BaseMarchingCubeGenerator : IDensityMapGenerator
{
    private IChunkConfiguration configuration;

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

    public virtual Dictionary<Vector3Int, MeshData> DispatchGeneration(List<ChunkContext> chunkContexts)
    {
        int batchSize = chunkContexts.Count;
        int size = Options.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * batchSize;

        var genShader = chunkContexts[0].Services.ChunkManager.GenerateDensity;
        var mcShader = chunkContexts[0].Services.ChunkManager.MarchingCubes;

        var chunkInputs = new List<ChunkInput>(batchSize);
        for (int i = 0; i < batchSize; i++)
        {
            var ctx = chunkContexts[i];
            chunkInputs.Add(new ChunkInput
            {
                CoordPos = ctx.Coordinates,
                WorldPos = ctx.WorldPosition,
                stepSize = 1 << ctx.LODIndex
            });
        }

        ComputeBuffer chunkInputBuffer = new ComputeBuffer(batchSize, Marshal.SizeOf<ChunkInput>());
        chunkInputBuffer.SetData(chunkInputs);

        ComputeBuffer densityBuffer = new ComputeBuffer(totalVoxels, sizeof(float));
        ComputeBuffer triangleBuffer = new ComputeBuffer(totalVoxels * 5, Marshal.SizeOf<Triangle>(), ComputeBufferType.Append);
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer.SetCounterValue(0);

        genShader.SetBuffer(0, "ChunkInputs", chunkInputBuffer);
        genShader.SetBuffer(0, "DensityMap", densityBuffer);
        genShader.SetInt("_SizeX", size);
        genShader.SetInt("_SizeY", size);
        genShader.SetInt("_SizeZ", size);
        genShader.SetFloat("_Seed", Options.Seed);
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
        genShader.Dispatch(0, batchSize * size, size, size);

        mcShader.SetBuffer(0, "DensityMap", densityBuffer);
        mcShader.SetBuffer(0, "TriangleBuffer", triangleBuffer);
        mcShader.SetBuffer(0, "ChunkInputs", chunkInputBuffer);
        mcShader.SetBuffer(0, "BiomeColors", BiomeBuffer);
        mcShader.SetInt("_BiomeCount", BiomeCount);
        mcShader.SetInt("_SizeX", size);
        mcShader.SetInt("_SizeY", size);
        mcShader.SetInt("_SizeZ", size);
        mcShader.SetFloat("_IsoLevel", Options.ISOLevel);
        mcShader.Dispatch(0, batchSize * 16, 16, 16);

        // Read back from GPU.
        ComputeBuffer.CopyCount(triangleBuffer, countBuffer, 0);
        int[] triCountArr = new int[1];
        countBuffer.GetData(triCountArr);
        int triCount = triCountArr[0];

        Triangle[] tris = new Triangle[triCount];
        triangleBuffer.GetData(tris);

        var meshes = ConvertToMeshes(tris, batchSize);

        // Cleanup
        densityBuffer.Dispose();
        triangleBuffer.Dispose();
        countBuffer.Dispose();
        chunkInputBuffer.Dispose();

        return meshes;
    }
    private Dictionary<Vector3Int, MeshData> ConvertToMeshes(Triangle[] tris, int chunkCount)
    {
        // Group triangles by their chunk index
        Dictionary<Vector3Int, List<Triangle>> chunkGroups = new();

        foreach (var triangle in tris)
        {
            Vector3Int coord = new Vector3Int((int)triangle.CoordPos.x, (int)triangle.CoordPos.y, (int)triangle.CoordPos.z);
            if (!chunkGroups.ContainsKey(coord))
                chunkGroups[coord] = new List<Triangle>();

            chunkGroups[coord].Add(triangle);
        }

        Dictionary<Vector3Int, MeshData> allMeshes = new(chunkGroups.Count);

        foreach (var kvp in chunkGroups)
        {
            List<Triangle> chunkTris = kvp.Value;
            List<Vector3> vertices = new List<Vector3>(chunkTris.Count * 3);
            List<int> indices = new List<int>(chunkTris.Count * 3);
            List<Color32> colors = new List<Color32>(chunkTris.Count * 3);
            List<Vector3> normals = new List<Vector3>(chunkTris.Count * 3);
            List<Vector2> uvs = new List<Vector2>(chunkTris.Count * 3); // optional

            foreach (var t in chunkTris)
            {
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

                // uvs.Add(t.UVA);
            }

            MeshData meshData = new MeshData(vertices, indices, normals, uvs);
            meshData.Colors = colors.ToArray();

            allMeshes.Add(kvp.Key, meshData);
        }

        return allMeshes;
    }
}