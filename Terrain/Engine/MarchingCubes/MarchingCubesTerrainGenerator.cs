using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// My marching-cubes generator. Feeds compute with chunk inputs, spits out a draw-ready batch.
/// Reuses a couple of lists to keep GC quiet. No GameObjects here, just GPU buffers.
/// </summary>
public class MarchingCubesTerrainGenerator : ITerrainGenerator
{
    private struct TerrainJob
    {
        public IReadOnlyList<ChunkKey> Keys;
        public Action<ChunkRenderBatch> Output;
    }

    // Hard caps I tune for my buckets. 1024 = surface mask scan, 128 = per-batch gen.
    private const int SurfaceCap = 1024;
    private const int GenerateCap = 128;

    private IChunkServices chunkServices;

    // Shaders doing the real work
    private ComputeShader GenerateShader;
    private ComputeShader MarchingShader;

    // Shared GPU state
    private ComputeBuffer DensityBuffer;           // RWStructuredBuffer<float>  (voxel scalar field)
    private ComputeBuffer SurfaceChunkInputBuffer; // StructuredBuffer<ChunkInput> for mask pass
    private ComputeBuffer GenerateChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
    private ComputeBuffer BiomeBuffer;             // StructuredBuffer<BiomeData> (small table)
    private ComputeBuffer DensityOptionsBuffer;    // StructuredBuffer<DensityMapOptions> (1 element)
    private ComputeBuffer PlanetOptionsBuffer;     // StructuredBuffer<PlanetDensityOptions> (1 element)
    private ComputeBuffer SurfaceMaskBuffer;       // RWStructuredBuffer<uint> (results for mask pass)

    private int BiomesCount = 0;

    // Reused staging lists -> no per-dispatch GC. Capacity matches caps above.
    private List<ChunkDispatchKey> InputSurface = new(SurfaceCap);
    private List<ChunkDispatchKey> InputGenerate = new(GenerateCap);

    // A bunch of buffers to help with GC problems.
    private List<ComputeBuffer> TriangleBuffers = new List<ComputeBuffer>();

    private List<TerrainJob> Jobs = new();

    /// <summary>
    /// Initialize a new instance of the <see cref="MarchingCubesTerrainGenerator"/> class.
    /// </summary>
    /// <param name="chunkServices"></param>
    /// <param name="generateShader"></param>
    /// <param name="marchingShader"></param>
    public MarchingCubesTerrainGenerator(IChunkServices chunkServices, ComputeShader generateShader, ComputeShader marchingShader)
    {
        this.chunkServices = chunkServices;
        GenerateShader = generateShader;
        MarchingShader = marchingShader;

        this.InitBuffer();
    }

    // Convenience snapshot so I don’t keep typing the long path.
    private TerrainDensityOptions densityOptions => chunkServices.Configuration.DensityOptions;

    /// <summary>
    /// Process multiple jobs from the queue to generate chunks.
    /// </summary>
    public void Update()
    {
        if (this.Jobs.Count == 0) return;

        for (int i = 0; i < 2; i++)
        {
            if (this.Jobs.Count == 0) break;

            ProcessBatch(Jobs[0].Keys, Jobs[0].Output); Jobs.RemoveAt(0);
        }
    }

    /// <summary>
    /// Dispose of the object.
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public void Dispose()
    {
        // TODO: make this idempotent + null-safe; for now just a reminder I need to wire it.
        // DensityBuffer?.Dispose(); etc…
        throw new System.NotImplementedException();
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
    /// Quick-and-dirty mask pass to cull empty chunks before we spend time meshing them.
    /// </summary>
    public void GetSurfaceMaskChecks(IReadOnlyList<ChunkGenerationJob> keys, Action<uint[]> output)
    {
        int batchSize = keys.Count;

        // Refill list + upload
        FillSurfaceChunkInputs(keys);

        // Kick the mask kernel
        GenerateShader.SetInt("_ChunkInputCount", batchSize);
        GenerateShader.SetBuffer(1, "ChunkInputs", SurfaceChunkInputBuffer);
        GenerateShader.SetBuffer(1, "DensityOptions", DensityOptionsBuffer);
        GenerateShader.SetBuffer(1, "PlanetOptions", PlanetOptionsBuffer);
        GenerateShader.SetBuffer(1, "SurfaceMask", SurfaceMaskBuffer);
        GenerateShader.Dispatch(1, Mathf.CeilToInt(batchSize / 64f), 1, 1);

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
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// </summary>
    private void ProcessBatch(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output)
    {
        int batchSize = keys.Count;
        int size = densityOptions.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * batchSize;

        // Fill the reusable input list + upload to the per-kernel input buffer.
        FillGenerateChunkInputs(keys);

        // Per-result buffers (owned by the returned batch; caller disposes)
        ComputeBuffer triangleBuffer = GetOrCreateBuffer();
        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        triangleBuffer.SetCounterValue(0);

        // Generate density
        GenerateShader.SetBuffer(0, "ChunkInputs", GenerateChunkInputBuffer);
        GenerateShader.SetBuffer(0, "DensityOptions", DensityOptionsBuffer);
        GenerateShader.SetBuffer(0, "PlanetOptions", PlanetOptionsBuffer);
        GenerateShader.SetBuffer(0, "DensityMap", DensityBuffer);

        // NOTE: thread group dims assume [numthreads(8,8,8)] and X packs chunkIndex*XWithinChunk
        GenerateShader.Dispatch(0,
            Mathf.CeilToInt(batchSize * size / 4f),
            Mathf.CeilToInt(size / 4f),
            Mathf.CeilToInt(size / 4f));

        // Marching cubes
        MarchingShader.SetBuffer(0, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(0, "DensityMap", DensityBuffer);
        MarchingShader.SetBuffer(0, "DensityOptions", DensityOptionsBuffer);
        MarchingShader.SetBuffer(0, "PlanetOptions", PlanetOptionsBuffer);
        MarchingShader.SetBuffer(0, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(0, "BiomeColors", BiomeBuffer);
        MarchingShader.SetInt("_BiomeCount", BiomesCount);
        MarchingShader.Dispatch(0, batchSize * 4, 4, 4);

        // Build indirect args from append count
        MarchingShader.SetBuffer(1, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(1, "ArgsBuffer", argsBuffer);
        MarchingShader.Dispatch(1, 1, 1, 1);

        // Hand back a draw-ready batch (triangles + args + bounds computed from keys)
        output.Invoke(new ChunkRenderBatch(triangleBuffer, argsBuffer, keys, this.chunkServices));
    }

    /// <summary>
    /// One-time GPU allocations sized for my current caps. If I change caps, I should reallocate.
    /// </summary>
    private void InitBuffer()
    {
        // Small table (I start with 5; UpdateOptions writes actual count)
        BiomeBuffer = new ComputeBuffer(5, Marshal.SizeOf<ChunkBiomeData>());

        // Single struct (Structured buffer of length 1)
        DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<TerrainDensityOptions>(), ComputeBufferType.Structured);
        PlanetOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<PlanetDensityOptions>(), ComputeBufferType.Structured);

        // Scalar field big enough for 128 chunks at current chunk size (rough over-alloc)
        int size = densityOptions.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int maxTotalVoxels = voxelCountPerChunk * GenerateCap;
        DensityBuffer = new ComputeBuffer(maxTotalVoxels, sizeof(float));

        // Per-kernel inputs + mask output
        SurfaceChunkInputBuffer = new ComputeBuffer(SurfaceCap, Marshal.SizeOf<ChunkDispatchKey>());
        GenerateChunkInputBuffer = new ComputeBuffer(GenerateCap, Marshal.SizeOf<ChunkDispatchKey>());
        SurfaceMaskBuffer = new ComputeBuffer(SurfaceCap, sizeof(uint));

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
        int size = densityOptions.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * 16;

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
}
