using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using WaveHarmonic.Crest;
using static UnityEditor.Rendering.CameraUI;
using static UnityEngine.XR.Interaction.Toolkit.Inputs.Interactions.SectorInteraction;

/// <summary>
/// My marching-cubes generator. Feeds compute with chunk inputs, spits out a draw-ready batch.
/// Reuses a couple of lists to keep GC quiet. No GameObjects here, just GPU buffers.
/// </summary>
public class MarchingCubesTerrainGenerator : ITerrainGenerator
{
    // Hard caps I tune for my buckets. 1024 = surface mask scan, 128 = per-batch gen.
    private const int SurfaceCap = 1024;
    private const int GenerateCap = 128;
    private const int JobsPerTick = 2;

    private struct TerrainJob
    {
        public IReadOnlyList<ChunkKey> Keys;
        public Action<ChunkRenderBatch> Output;
    }

    private IChunkServices chunkServices;

    // Shaders doing the real work
    private ComputeShader MarchingShader;

    // Shared GPU state
    private ComputeBuffer DensityBuffer;           // RWStructuredBuffer<float>  (voxel scalar field)
    private ComputeBuffer SurfaceChunkInputBuffer; // StructuredBuffer<ChunkInput> for mask pass
    private ComputeBuffer GenerateChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
    private ComputeBuffer EdgeGenerateChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
    private ComputeBuffer EdgeChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
    private ComputeBuffer EdgeNeighborChunkBuffer;
    private ComputeBuffer BiomeBuffer;             // StructuredBuffer<BiomeData> (small table)
    private ComputeBuffer DensityOptionsBuffer;    // StructuredBuffer<DensityMapOptions> (1 element)
    private ComputeBuffer PlanetOptionsBuffer;     // StructuredBuffer<PlanetDensityOptions> (1 element)
    private ComputeBuffer SurfaceMaskBuffer;       // RWStructuredBuffer<uint> (results for mask pass)

    // Annoying triangle tables.
    private ComputeBuffer CornerOffsetsBuffer = MarchingCubesTables.CornerOffsetsBuffer();
    private ComputeBuffer EdgeConnectionsBuffer = MarchingCubesTables.EdgeConnectionsBuffer();
    private ComputeBuffer TriangleTableBuffer = MarchingCubesTables.TriangleTableBuffer();

    // Material for chunks
    private Material chunkMaterial;

    private int BiomesCount = 0;

    // Reused staging lists -> no per-dispatch GC. Capacity matches caps above.
    private List<ChunkDispatchKey> InputSurface = new(SurfaceCap);
    private List<ChunkDispatchKey> InputGenerate = new(GenerateCap);

    // A bunch of buffers to help with GC problems.
    // (Before keeping a collection of buffers there was a very small, but very noticeable stutter on large collections)
    private List<ComputeBuffer> TriangleBuffers = new List<ComputeBuffer>();

    private List<TerrainJob> Jobs = new();
    private List<TerrainJob> EdgeJobs = new();

    /// <summary>
    /// Initialize a new instance of the <see cref="MarchingCubesTerrainGenerator"/> class.
    /// </summary>
    /// <param name="chunkServices"></param>
    /// <param name="generateShader"></param>
    /// <param name="marchingShader"></param>
    public MarchingCubesTerrainGenerator(IChunkServices chunkServices, ComputeShader marchingShader)
    {
        this.chunkServices = chunkServices;
        MarchingShader = marchingShader;

        this.chunkMaterial = new Material(Shader.Find("Custom/URP_CustomLitGPU"));
        this.chunkMaterial.SetFloat("_Smoothness", 0f);
        this.chunkMaterial.SetFloat("_UseVertexColor", 1f);

        this.InitBuffer();
    }

    /// <summary>
    /// Convenience snapshot so I don’t keep typing the long path.
    /// </summary>
    private TerrainDensityOptions densityOptions => chunkServices.Configuration.DensityOptions;

    /// <summary>
    /// Get the custom material used in generation.
    /// </summary>
    public Material GetMaterial
    {
        get {  return chunkMaterial; }
        private set {  chunkMaterial = value; }
    }

    /// <summary>
    /// Process multiple jobs from the queue to generate chunks.
    /// </summary>
    public void Update()
    {
        if (this.Jobs.Count == 0) return;

        // Two normal jobs.
        for (int i = 0; i < JobsPerTick; i++)
        {
            if (this.Jobs.Count == 0) break;

            ProcessBatch(Jobs[0].Keys, Jobs[0].Output); Jobs.RemoveAt(0);
        }

        // Two edge jobs.
        for (int i = 0; i < JobsPerTick; i++)
        {
            if (this.EdgeJobs.Count == 0) break;

            ProcessEdgeBatches(EdgeJobs[0].Keys, EdgeJobs[0].Output); EdgeJobs.RemoveAt(0);
        }
    }

    /// <summary>
    /// Dispose of the object.
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public void Dispose()
    {
        DensityBuffer.Dispose();
        SurfaceChunkInputBuffer.Dispose();
        GenerateChunkInputBuffer.Dispose();
        BiomeBuffer.Dispose();
        DensityOptionsBuffer.Dispose();
        PlanetOptionsBuffer.Dispose();
        SurfaceMaskBuffer.Dispose();

        CornerOffsetsBuffer.Dispose();
        EdgeConnectionsBuffer.Dispose();
        TriangleTableBuffer.Dispose();

        chunkMaterial = null;
        InputSurface.Clear();
        InputGenerate.Clear();

        foreach (var triangle in TriangleBuffers)
            triangle.Dispose();

        TriangleBuffers.Clear();

        Jobs.Clear();
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// This job will be queued and ran in a further update to reduce GPU pressure.
    /// </summary>
    public void GenerateBatch(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output)
    {
        this.Jobs.Add(new TerrainJob() { Keys = keys, Output = output });
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch along with any
    /// respective neighbors so we can stitch them together, runs MC, returns triangle + args buffers.
    /// This job will be queued and ran in a further update to reduce GPU pressure.
    /// </summary>
    public void GenerateEdgeBatch(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output)
    {
        this.EdgeJobs.Add(new TerrainJob() { Keys = keys, Output = output });
    }

    /// <summary>
    /// Quick-and-dirty mask pass to cull empty chunks before we spend time meshing them.
    /// </summary>
    public void GetSurfaceMaskChecks(IReadOnlyList<ChunkGenerationJob> keys, Action<uint[]> output)
    {
        int batchSize = keys.Count;

        // Refill list + upload
        FillSurfaceChunkInputs(keys);

        int kernalId = MarchingShader.FindKernel("GenerateSurfaceMask");

        // Kick the mask kernel
        MarchingShader.SetBuffer(kernalId, "ChunkInputs", SurfaceChunkInputBuffer);
        MarchingShader.SetBuffer(kernalId, "SurfaceMask", SurfaceMaskBuffer);
        MarchingShader.Dispatch(kernalId, batchSize, 1, 1);

        var req = AsyncGPUReadback.Request(SurfaceMaskBuffer, r =>
        {
            if (!r.hasError)
            {
                output.Invoke(r.GetData<uint>().ToArray());
            }
        });
    }

    /// <summary>
    /// Push runtime/editor option changes to GPU. Call when sliders change.
    /// </summary>
    public void UpdateOptions()
    {
        // Options buffer is a single struct; just overwrite it.
        DensityOptionsBuffer.SetData(new[] { densityOptions });
        PlanetOptionsBuffer.SetData(new[] { this.chunkServices.Configuration.PlanetOptions });

        // Rebuild biome table (small) and upload.
        var biomes = chunkServices.Configuration.Biomes.OrderBy(b => b.MinSurface).ToList();
        BiomesCount = biomes.Count;

        var biomeData = new ChunkBiomeData[biomes.Count];
        for (int i = 0; i < biomes.Count; i++)
        {
            // NOTE: I grab first/last color key; good enough for now.
            biomeData[i] = new ChunkBiomeData
            {
                MinSurface = biomes[i].MinSurface,
                MaxSurface = biomes[i].MaxSurface,
                GradientStart = biomes[i].SurfaceColorRange.colorKeys[0].color,
                GradientEnd = biomes[i].SurfaceColorRange.colorKeys[^1].color,
            };
        }

        BiomeBuffer.SetData(biomeData);

        // Update material.
        this.chunkMaterial.SetBuffer("BiomeColors", BiomeBuffer);
        this.chunkMaterial.SetInt("_BiomeCount", BiomesCount);
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// </summary>
    private void ProcessBatch(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output)
    {
        int batchSize = keys.Count;
        int size = GetChunkSize();
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * batchSize;

        // Fill the reusable input list + upload to the per-kernel input buffer.
        FillGenerateChunkInputs(keys);

        // Per-result buffers (owned by the returned batch; caller disposes)
        ComputeBuffer triangleBuffer = GetOrCreateBuffer();
        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        triangleBuffer.SetCounterValue(0);

        int genKernal = MarchingShader.FindKernel("GenerateDensityMap");
        int marchKernal = MarchingShader.FindKernel("RunMarchingCubes");
        int argsKernal = MarchingShader.FindKernel("PrepareDrawArgs");

        // Generate density
        MarchingShader.SetBuffer(genKernal, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(genKernal, "DensityMap", DensityBuffer);

        // NOTE: thread group dims assume [numthreads(8,8,8)] and X packs chunkIndex*XWithinChunk
        MarchingShader.Dispatch(genKernal,
            Mathf.CeilToInt(batchSize * size / 4f),
            Mathf.CeilToInt(size / 4f),
            Mathf.CeilToInt(size / 4f));

        // Marching cubes
        MarchingShader.SetBuffer(marchKernal, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(marchKernal, "DensityMap", DensityBuffer);
        MarchingShader.SetBuffer(marchKernal, "TriangleBuffer", triangleBuffer);
        MarchingShader.Dispatch(marchKernal, batchSize * 4, 4, 4);

        // Build indirect args from append count
        MarchingShader.SetBuffer(argsKernal, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(argsKernal, "ArgsBuffer", argsBuffer);
        MarchingShader.Dispatch(argsKernal, 1, 1, 1);

        // Hand back a draw-ready batch (triangles + args + bounds computed from keys)
        output.Invoke(new ChunkRenderBatch(triangleBuffer, argsBuffer, keys, this.chunkServices));
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="output"></param>
    private void ProcessEdgeBatches(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output)
    {
        ComputeBuffer triangleBuffer = GetOrCreateBuffer();
        triangleBuffer.SetCounterValue(0);

        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        foreach (var key in keys)
        {
            EdgeDirection edges = this.chunkServices.Layout.GetLODEdges(key);
            if (edges == EdgeDirection.None)
                continue;

            List<ChunkKey> neighborKeys = new();
            List<ChunkEdgeNeighbor> neighborEdges = new();
            int index = 0;

            // This is our parent.
            neighborKeys.Add(key);

            // Retrieve the neighbors.
            foreach (var pair in EdgeDirectionHelper.DirectionOffsets)
            {
                if ((edges & pair.Key) != 0)
                {
                    neighborEdges.Add(new ChunkEdgeNeighbor(pair.Value, index, (int)pair.Key, key.LODIndex - 1));
                    neighborKeys.Add(new ChunkKey(key.Coordinates + pair.Value, key.LODIndex));

                    index++;
                }
            }

            // Process.
            ProcessEdgeBatch(key, neighborKeys,  neighborEdges, triangleBuffer, output);
        }

        // Build indirect args from append count
        int argsKernal = MarchingShader.FindKernel("PrepareDrawArgs");
        MarchingShader.SetBuffer(argsKernal, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(argsKernal, "ArgsBuffer", argsBuffer);
        MarchingShader.Dispatch(argsKernal, 1, 1, 1);

        output.Invoke(new ChunkRenderBatch(triangleBuffer, argsBuffer, keys, this.chunkServices));
    }

    /// <summary>
    /// Process an Edge batch to rendered.
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="keys"></param>
    private void ProcessEdgeBatch(ChunkKey parent, List<ChunkKey> keys, List<ChunkEdgeNeighbor> neighborEdges, ComputeBuffer triangleBuffer, Action<ChunkRenderBatch> output)
    {
        int batchSize = keys.Count;
        int size = GetChunkSize();
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * batchSize;

        // Fill the reusable input list + upload to the per-kernel input buffer.
        FillGenerateChunkInputs(keys);
        EdgeNeighborChunkBuffer.SetData(neighborEdges.ToArray());

        int genKernal = MarchingShader.FindKernel("GenerateDensityMap");
        int marchKernal = MarchingShader.FindKernel("RunMarchingCubesStitch");

        // Generate density
        MarchingShader.SetBuffer(genKernal, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(genKernal, "DensityMap", DensityBuffer);

        // NOTE: thread group dims assume [numthreads(8,8,8)] and X packs chunkIndex*XWithinChunk
        MarchingShader.Dispatch(genKernal,
            Mathf.CeilToInt(batchSize * size / 4f),
            Mathf.CeilToInt(size / 4f),
            Mathf.CeilToInt(size / 4f));

        // We only want to do marching for our one input.
        EdgeGenerateChunkInputBuffer.SetData(new[] { parent }, 0, 0, 1);

        // Marching cubes
        MarchingShader.SetBuffer(marchKernal, "Neighbors", EdgeNeighborChunkBuffer);
        MarchingShader.SetInt("NeighborsCount", neighborEdges.Count);
        MarchingShader.SetBuffer(marchKernal, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(marchKernal, "DensityMap", DensityBuffer);
        MarchingShader.SetBuffer(marchKernal, "TriangleBuffer", triangleBuffer);
        MarchingShader.Dispatch(marchKernal, size / 4, size / 4, size / 4);
    }

    /// <summary>
    /// One-time GPU allocations sized for my current caps. If I change caps, I should reallocate.
    /// </summary>
    private void InitBuffer()
    {
        // Small table (I start with 5; UpdateOptions writes actual count)
        BiomeBuffer = new ComputeBuffer(5, Marshal.SizeOf<ChunkBiomeData>());

        // Single struct (Structured buffer of length 1)
        DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<TerrainDensityOptions>(), ComputeBufferType.Constant);
        PlanetOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<PlanetDensityOptions>(), ComputeBufferType.Constant);

        DensityOptionsBuffer.SetData(new[] { densityOptions });
        PlanetOptionsBuffer.SetData(new[] { this.chunkServices.Configuration.PlanetOptions });

        this.MarchingShader.SetConstantBuffer("TerrainDensityOptions", DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());
        this.MarchingShader.SetConstantBuffer("PlanetDensityOptions", PlanetOptionsBuffer, 0, Marshal.SizeOf<PlanetDensityOptions>());

        // Scalar field big enough for 128 chunks at current chunk size (rough over-alloc)
        int size = GetChunkSize();
        int voxelCountPerChunk = size * size * size;
        int maxTotalVoxels = voxelCountPerChunk * GenerateCap;
        DensityBuffer = new ComputeBuffer(maxTotalVoxels, sizeof(float));

        // Per-kernel inputs + mask output
        SurfaceChunkInputBuffer = new ComputeBuffer(SurfaceCap, Marshal.SizeOf<ChunkDispatchKey>());
        GenerateChunkInputBuffer = new ComputeBuffer(GenerateCap, Marshal.SizeOf<ChunkDispatchKey>());
        EdgeGenerateChunkInputBuffer = new ComputeBuffer(GenerateCap, Marshal.SizeOf<ChunkDispatchKey>());
        SurfaceMaskBuffer = new ComputeBuffer(SurfaceCap, sizeof(uint));
        EdgeNeighborChunkBuffer = new ComputeBuffer(8, Marshal.SizeOf<ChunkEdgeNeighbor>());

        // Set static buffers
        int marchKernal = MarchingShader.FindKernel("RunMarchingCubes");
        MarchingShader.SetBuffer(marchKernal, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(marchKernal, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(marchKernal, "TriangleTableBuffer", TriangleTableBuffer);

        int marchKernal1 = MarchingShader.FindKernel("RunMarchingCubesStitch");
        MarchingShader.SetBuffer(marchKernal1, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(marchKernal1, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(marchKernal1, "TriangleTableBuffer", TriangleTableBuffer);

        // Prime options/biomes on GPU
        UpdateOptions();
    }

    /// <summary>
    /// Refill the reusable surface-input list and upload. No allocations here on repeat calls.
    /// </summary>
    private void FillSurfaceChunkInputs(IReadOnlyList<ChunkGenerationJob> keys)
    {
        int n = keys.Count;

        // Keep backing array; just reset count.
        InputSurface.Clear();
        InputSurface.EnsureCapacity(n); // avoid growth churn if we spike

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i];
            InputSurface.Add(new ChunkDispatchKey
            {
                CoordPos = ctx.Key.Coordinates,
                LodIndex = ctx.Key.LODIndex
            });
        }

        // Upload only the live span [0..n).
        SurfaceChunkInputBuffer.SetData(InputSurface, 0, 0, n);
    }

    /// <summary>
    /// Refill the reusable generate-input list and upload. Same deal as surface path.
    /// </summary>
    private void FillGenerateChunkInputs(IReadOnlyList<ChunkKey> keys)
    {
        int n = keys.Count;

        InputGenerate.Clear();
        InputGenerate.EnsureCapacity(n);

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i];
            InputGenerate.Add(new ChunkDispatchKey
            {
                CoordPos = ctx.Coordinates,
                LodIndex = ctx.LODIndex
            });
        }

        GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, n);
    }

    /// <summary>
    /// Create a new triangle buffer.
    /// </summary>
    /// <returns></returns>
    private ComputeBuffer CreateTriangleBuffer()
    {
        int size = GetChunkSize();
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = (voxelCountPerChunk * GetChunkSize());

        var newBuff = new ComputeBuffer(totalVoxels, Marshal.SizeOf<ChunkTriangleData>(), ComputeBufferType.Append);

        return newBuff;
    }

    /// <summary>
    /// Gets or creates a new triangle buffer, like a great value pooled object helps with runtime GC issues.
    /// </summary>
    /// <returns></returns>
    private ComputeBuffer GetOrCreateBuffer()
    {
        if (TriangleBuffers.Count == 0)
        {
            for (int i = 0; i < 100; i++)
            {
                this.TriangleBuffers.Add(CreateTriangleBuffer());
            }

            return GetOrCreateBuffer();
        }

        var buf = TriangleBuffers[0];
        TriangleBuffers.RemoveAt(0);
        return buf;
    }

    private int GetChunkSize()
    {
        return densityOptions.ChunkSize + 3;
    }
}

