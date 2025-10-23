using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.Experimental.GraphView.Port;

/// <summary>
/// My marching-cubes generator. Feeds compute with chunk inputs, spits out a draw-ready batch.
/// Reuses a couple of lists to keep GC quiet. No GameObjects here, just GPU buffers.
/// </summary>
public class MarchingCubesChunkGenerator : IChunkGenerator
{
    // Hard caps I tune for my buckets. 1024 = surface mask scan, 128 = per-batch gen.
    private const int SurfaceCap = 1024;
    private const int GenerateCap = 128;
    private const int JobsPerTick = 6;

    private struct TerrainJob
    {
        public IReadOnlyList<ChunkKey> Keys;
        public Action<ChunkRenderBatch> Output;
        public ChunkRenderBatch ExistingBatch;
    }

    private IChunkServices chunkServices;

    // Shaders doing the real work
    private ComputeShader MarchingShader;

    // Shared GPU state
    private ComputeBuffer DensityBuffer;           // RWStructuredBuffer<float>  (voxel scalar field)
    private ComputeBuffer SurfaceChunkInputBuffer; // StructuredBuffer<ChunkInput> for mask pass
    private ComputeBuffer GenerateChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
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
    private List<ChunkDispatchKeyGPU> InputSurface = new(SurfaceCap);
    private List<ChunkDispatchKeyGPU> InputGenerate = new(GenerateCap);

    // Kernel ID's.
    private int surfaceKernel;
    private int genKernel;
    private int marchKernel;
    private int detailKernel;
    private int argsKernel;

    private List<TerrainJob> Jobs = new();

    /// <summary>
    /// Initialize a new instance of the <see cref="MarchingCubesChunkGenerator"/> class.
    /// </summary>
    /// <param name="chunkServices"></param>
    /// <param name="generateShader"></param>
    /// <param name="marchingShader"></param>
    public MarchingCubesChunkGenerator(IChunkServices chunkServices, ComputeShader marchingShader, Material chunkMat)
    {
        this.chunkServices = chunkServices;
        MarchingShader = marchingShader;

        this.chunkMaterial = chunkMat;

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
        get { return chunkMaterial; }
        private set { chunkMaterial = value; }
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

            ProcessBatch(Jobs[0].Keys, Jobs[0].Output, Jobs[0].ExistingBatch); Jobs.RemoveAt(0);
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

        Jobs.Clear();
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// This job will be queued and ran in a further update to reduce GPU pressure.
    /// </summary>
    public void DispatchGeneration(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
    {
        this.Jobs.Add(new TerrainJob() { Keys = keys, Output = output, ExistingBatch = existingBatch });
    }

    /// <summary>
    /// Quick-and-dirty mask pass to cull empty chunks before we spend time meshing them.
    /// </summary>
    public void DispatchSurfaceChecks(IReadOnlyList<ChunkGenerationJob> keys, Action<uint[]> output)
    {
        int batchSize = keys.Count;

        // Refill list + upload
        FillSurfaceChunkInputs(keys);

        // Kick the mask kernel
        MarchingShader.SetBuffer(surfaceKernel, "ChunkInputs", SurfaceChunkInputBuffer);
        MarchingShader.SetBuffer(surfaceKernel, "SurfaceMask", SurfaceMaskBuffer);

        MarchingShader.Dispatch(surfaceKernel, batchSize, 1, 1);

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
        var biomes = chunkServices.Configuration.BiomeLibrary.Biomes.ToList();
        BiomesCount = biomes.Count;

        var biomeData = new ChunkBiomeGPU[biomes.Count];
        for (int i = 0; i < biomes.Count; i++)
        {
            biomeData[i] = new ChunkBiomeGPU
            {
                Height = (uint)biomes[i].Height,
                Temperature = (uint)biomes[i].Temperature,
                Humidity = (uint)biomes[i].Humidity,
                Foliage = (uint)biomes[i].Foliage,
                Highlight = biomes[i].Highlight,
                Light = biomes[i].Light,
                MidLight = biomes[i].MidLight,
                Mid = biomes[i].Mid,
                Dark = biomes[i].Dark,
                Shadow = biomes[i].Shadow
            };
        }

        BiomeBuffer.SetData(biomeData);

        // Update material.
        this.chunkMaterial.SetBuffer("Biomes", BiomeBuffer);
        this.chunkMaterial.SetInt("_BiomesCount", BiomesCount);
        this.chunkMaterial.SetInt("Overlay", (int)this.chunkServices.Configuration.DebugOptions.Overlay);

        this.chunkMaterial.SetFloat("_UseVertexColor", 1f);
        this.chunkMaterial.SetVector("PositionOffset", chunkServices.Configuration.DensityOptions.PositionOffset);
        this.chunkMaterial.SetVector("PlanetCenter", chunkServices.Configuration.PlanetOptions.PlanetCenter);
        this.chunkMaterial.SetFloat("PlanetRadius", chunkServices.Configuration.PlanetOptions.PlanetRadius);
        this.chunkMaterial.SetInt("SubVariant", (int)chunkServices.Configuration.DensityOptions.TerrainType);
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// </summary>
    private void ProcessBatch(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
    {
        int batchSize = keys.Count;

        int cubesPerAxis = CubesPerAxis;
        int samplesPerAxis = CubesPerAxis + 1 + (2 * BorderSamples);

        int cubesPerChunk = cubesPerAxis * cubesPerAxis * cubesPerAxis;
        int samplesPerChunk = samplesPerAxis * samplesPerAxis * samplesPerAxis;

        int cubesSize = cubesPerAxis * batchSize;
        int samplesSize = samplesPerAxis * batchSize;

        if (batchSize == 0)
            return;

        // Fill the reusable input list + upload to the per-kernel input buffer.
        FillGenerateChunkInputs(keys);

        ComputeBuffer triangleBuffer = existingBatch?.Triangle;
        ComputeBuffer detailBuffer = existingBatch?.Details;
        ComputeBuffer argsBuffer = existingBatch?.Args;

        if (existingBatch == null)
        {
            triangleBuffer = new ComputeBuffer(60000, Marshal.SizeOf<TriangleDataGPU>(), ComputeBufferType.Append);
            detailBuffer = new ComputeBuffer(60000, Marshal.SizeOf<ChunkDetailDataGPU>(), ComputeBufferType.Append | ComputeBufferType.Structured);
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        triangleBuffer.SetCounterValue(0);
        detailBuffer.SetCounterValue(0);

        // Generate density
        MarchingShader.SetBuffer(genKernel, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(genKernel, "DensityMap", DensityBuffer);

        // NOTE: thread group dims assume [numthreads(4,4,4)] and X packs chunkIndex*XWithinChunk
        int genGroupSize = Mathf.CeilToInt(samplesPerAxis / 4f);
        MarchingShader.Dispatch(genKernel, batchSize * genGroupSize, genGroupSize, genGroupSize);

        // Marching cubes
        MarchingShader.SetBuffer(marchKernel, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(marchKernel, "DensityMap", DensityBuffer);
        MarchingShader.SetBuffer(marchKernel, "InitialDetailBuffer", detailBuffer);
        MarchingShader.SetBuffer(marchKernel, "TriangleBuffer", triangleBuffer);

        int marchGroupSize = Mathf.CeilToInt(cubesPerAxis / 4f);
        MarchingShader.Dispatch(marchKernel, batchSize * marchGroupSize, marchGroupSize, marchGroupSize);

        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(triangleBuffer, countBuffer, 0);

        // Build indirect args from append count
        MarchingShader.SetBuffer(argsKernel, "CountBuffer", countBuffer);
        MarchingShader.SetBuffer(argsKernel, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(argsKernel, "ArgsBuffer", argsBuffer);
        MarchingShader.Dispatch(argsKernel, 1, 1, 1);

        MarchingShader.SetBuffer(detailKernel, "Biomes", BiomeBuffer);
        MarchingShader.SetInt("_BiomesCount", BiomesCount);
        MarchingShader.SetBuffer(detailKernel, "ArgsBuffer", argsBuffer);
        MarchingShader.SetBuffer(detailKernel, "DetailTriangles", triangleBuffer);
        MarchingShader.SetBuffer(detailKernel, "DetailBuffer", detailBuffer);

        int maxTriangles = (cubesPerAxis * cubesPerAxis * cubesPerAxis * 5 * batchSize);
        int groupsX = Mathf.CeilToInt(maxTriangles / 64f);
        MarchingShader.Dispatch(detailKernel, groupsX, 1,1);

        uint[] args = new uint[5];
        argsBuffer.GetData(args);
        uint triCount = args[0] / 3;
        Debug.Log($"LOD{keys[0].LODIndex} - Chunks {batchSize} - Triangles written: {triCount}, triangle capacity of buffer {triangleBuffer.count}");


        // Hand back a draw-ready batch (triangles + args + bounds computed from keys)
        output.Invoke(new ChunkRenderBatch(triangleBuffer, detailBuffer, argsBuffer, keys, this.chunkServices));
    }

    /// <summary>
    /// One-time GPU allocations sized for my current caps. If I change caps, I should reallocate.
    /// </summary>
    private void InitBuffer()
    {
        // Small table (I start with 5; UpdateOptions writes actual count)
        BiomeBuffer = new ComputeBuffer(chunkServices.Configuration.BiomeLibrary.Biomes.Count, Marshal.SizeOf<ChunkBiomeGPU>());

        // Single struct (Structured buffer of length 1)
        DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<TerrainDensityOptions>(), ComputeBufferType.Constant);
        PlanetOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<PlanetDensityOptions>(), ComputeBufferType.Constant);

        DensityOptionsBuffer.SetData(new[] { densityOptions });
        PlanetOptionsBuffer.SetData(new[] { this.chunkServices.Configuration.PlanetOptions });

        this.MarchingShader.SetConstantBuffer("TerrainDensityOptions", DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());
        this.MarchingShader.SetConstantBuffer("PlanetDensityOptions", PlanetOptionsBuffer, 0, Marshal.SizeOf<PlanetDensityOptions>());

        // Scalar field big enough for 128 chunks at current chunk size (rough over-alloc)
        int samples = CubesPerAxis + 1 + (2 * BorderSamples);
        int sampleCountPerChunk = samples * samples * samples;
        int maxTotalSamples = sampleCountPerChunk * GenerateCap;
        DensityBuffer = new ComputeBuffer(maxTotalSamples, sizeof(float));

        // Per-kernel inputs + mask output
        SurfaceChunkInputBuffer = new ComputeBuffer(SurfaceCap, Marshal.SizeOf<ChunkDispatchKeyGPU>());
        GenerateChunkInputBuffer = new ComputeBuffer(GenerateCap, Marshal.SizeOf<ChunkDispatchKeyGPU>());
        SurfaceMaskBuffer = new ComputeBuffer(SurfaceCap, sizeof(uint));

        genKernel = MarchingShader.FindKernel("GenerateDensityMap");
        marchKernel = MarchingShader.FindKernel("RunMarchingCubes");
        detailKernel = MarchingShader.FindKernel("RunDetailsPass");
        argsKernel = MarchingShader.FindKernel("PrepareDrawArgs");
        surfaceKernel = MarchingShader.FindKernel("GenerateSurfaceMask");

        // Set static buffers
        MarchingShader.SetBuffer(marchKernel, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(marchKernel, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(marchKernel, "TriangleTableBuffer", TriangleTableBuffer);

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

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i];
            InputSurface.Add(new ChunkDispatchKeyGPU
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

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i];
            InputGenerate.Add(new ChunkDispatchKeyGPU
            {
                CoordPos = ctx.Coordinates,
                LodIndex = ctx.LODIndex
            });
        }

        GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, n);
    }

    public int CubesPerAxis => densityOptions.CubesPerAxis;
    public int BorderSamples => densityOptions.BorderSamplesPerAxis;
}
