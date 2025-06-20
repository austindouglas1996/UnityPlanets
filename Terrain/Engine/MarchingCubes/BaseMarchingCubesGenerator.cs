using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.GPUSort;

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
        Material mat = new Material(Shader.Find("Custom/URP_CustomLitGPU"));
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_UseVertexColor", 1f);
        this.vertexMat = mat;

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

    public virtual void DispatchGeneration(List<ChunkContext> chunkContexts)
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

        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        // Copy triangle count from append buffer
        ComputeBuffer.CopyCount(triangleBuffer, argsBuffer, 0);

        // Now update the values
        uint[] args = new uint[5];
        argsBuffer.GetData(args);  // Read triangle count

        args[0] *= 3; // Convert triangle count to vertex count
        args[1] = 1;  // Instance count

        argsBuffer.SetData(args);  // Write back

        this.gpuSets.Add(new GPUSet(triangleBuffer, argsBuffer, chunkContexts));


        densityBuffer.Dispose();
        countBuffer.Dispose();
        chunkInputBuffer.Dispose();
    }

    public void DrawGizmo()
    {
        foreach (var set in gpuSets)
        {
            //Gizmos.color = Color.yellow;
            //Gizmos.DrawWireCube(set.Bounds.center, set.Bounds.size);
        }
    }

    private class GPUSet
    {
        public ComputeBuffer Triangle;
        public ComputeBuffer Args;
        public Bounds Bounds;

        public GPUSet(ComputeBuffer Triangle, ComputeBuffer Args, List<ChunkContext> contexts)
        {
            this.Triangle = Triangle;
            this.Args = Args;
            this.Bounds = this.ComputeBounds(contexts);
        }

        Bounds ComputeBounds(List<ChunkContext> chunkContexts)
        {
            if (chunkContexts.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 min = chunkContexts[0].WorldPosition;
            Vector3 max = chunkContexts[0].WorldPosition;

            foreach (var ctx in chunkContexts)
            {
                Vector3 pos = ctx.WorldPosition;
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = (max - min) + Vector3.one * 16;

            return new Bounds(center, size);
        }

        public void Dispose()
        {
            Args.Dispose();
            Triangle.Dispose();
        }

    }

    private List<GPUSet> gpuSets = new();
    private Material vertexMat;

    public void Draw()
    {
        uint[] args = new uint[5];

        foreach (var gpuSet in gpuSets)
        {
            gpuSet.Args.GetData(args);
            if (args[0] == 0) continue;

            vertexMat.SetBuffer("_TriangleBuffer", gpuSet.Triangle);
            vertexMat.SetPass(0);
            Graphics.DrawProceduralIndirectNow(
                MeshTopology.Triangles,
                gpuSet.Args,
                0);
        }
    }

    public void Dispose()
    {
        foreach (var set in gpuSets)
            set.Dispose();
        gpuSets.Clear();
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