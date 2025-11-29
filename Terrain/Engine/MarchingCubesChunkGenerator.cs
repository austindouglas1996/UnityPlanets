using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// My marching-cubes generator. Feeds compute with chunk inputs, spits out a draw-ready batch.
/// Reuses a couple of lists to keep GC quiet. No GameObjects here, just GPU buffers.
/// </summary>
public class MarchingCubesChunkGenerator : IChunkGenerator
{
    // Hard caps I tune for my buckets. 1024 = surface mask scan, 128 = per-batch gen.
    private const int SurfaceCap = 512;
    private const int GenerateCap = 64;

    private IChunkServices chunkServices;

    // Shaders doing the real work
    private ComputeShader MarchingShader;

    // Shared GPU state
    private ComputeBuffer SurfaceChunkInputBuffer; // StructuredBuffer<ChunkInput> for mask pass
    private ComputeBuffer GenerateChunkInputBuffer;// StructuredBuffer<ChunkInput> for full gen
    private ComputeBuffer BiomeBuffer;             // StructuredBuffer<BiomeData> (small table)
    private ComputeBuffer DensityOptionsBuffer;    // StructuredBuffer<DensityMapOptions> (1 element)
    private ComputeBuffer PlanetOptionsBuffer;     // StructuredBuffer<PlanetDensityOptions> (1 element)
    private ComputeBuffer SurfaceMaskBuffer;       // RWStructuredBuffer<uint> (results for mask pass)
    private ComputeBuffer countBuffer;

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
    private uint[] surfaceMaskCache = new uint[SurfaceCap];

    // Kernel ID's.
    private int ClearRange;
    private int GenerateSurfaceMask;
    private int GenerateDensityMap;
    private int RunMarchingCubesPrePass;
    private int RunMarchingCubes;
    private int RunRepackPrePass;
    private int RunRepack;
    private int RunDetailsPass;

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

    public void Update()
    {

    }

    /// <summary>
    /// Dispose of the object.
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public void Dispose()
    {
        SurfaceChunkInputBuffer.Dispose();
        GenerateChunkInputBuffer.Dispose();
        BiomeBuffer.Dispose();
        DensityOptionsBuffer.Dispose();
        PlanetOptionsBuffer.Dispose();
        SurfaceMaskBuffer.Dispose();
        countBuffer.Dispose();

        CornerOffsetsBuffer.Dispose();
        EdgeConnectionsBuffer.Dispose();
        TriangleTableBuffer.Dispose();

        chunkMaterial = null;
        InputSurface.Clear();
        InputGenerate.Clear();
    }

    /// <summary>
    /// Full marching-cubes path. Builds density for the batch, runs MC, returns triangle + args buffers.
    /// This job will be queued and ran in a further update to reduce GPU pressure.
    /// </summary>
    public void DispatchGeneration(ChunkKey?[] keys, int keyCount, Dictionary<int, ChunkKey?> modifications, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
    {
        ProcessBatch(keys, keyCount, modifications, output, existingBatch);
    }

    /// <summary>
    /// Quick-and-dirty mask pass to cull empty chunks before we spend time meshing them.
    /// </summary>
    public void DispatchSurfaceChecks(IReadOnlyList<ChunkGenerationJob> keys, Action<uint[]> output)
    {

        ConsoleTimer.Start("MC.Surface");

        int batchSize = keys.Count;

        // Refill list + upload
        FillSurfaceChunkInputs(keys);

        // Kick the mask kernel
        MarchingShader.SetBuffer(GenerateSurfaceMask, "ChunkInputs", SurfaceChunkInputBuffer);
        MarchingShader.SetBuffer(GenerateSurfaceMask, "SurfaceMask", SurfaceMaskBuffer);

        MarchingShader.Dispatch(GenerateSurfaceMask, batchSize, 1, 1);

        AsyncGPUReadback.Request(SurfaceMaskBuffer, r =>
        {
            if (r.hasError) return;

            var src = r.GetData<uint>();
            int count = Mathf.Min(src.Length, surfaceMaskCache.Length);

            // Copy into the existing array (no new allocations)
            src.Slice(0, count).CopyTo(surfaceMaskCache);

            // Now process or invoke the callback on a background thread
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    output?.Invoke(surfaceMaskCache);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
        });

        ConsoleTimer.Stop("MC.Surface");
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

        // Set buffer
        MarchingShader.SetBuffer(RunDetailsPass, "Biomes", BiomeBuffer);
        MarchingShader.SetInt("_BiomesCount", BiomesCount);

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
    private void ProcessBatch(ChunkKey?[] keys, int keyCount, Dictionary<int, ChunkKey?> mods, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
    {
        int batchSize = keyCount;

        int cubesPerAxis = CubesPerAxis;
        int samplesPerAxis = CubesPerAxis + 1 + (2 * BorderSamples);

        int cubesPerChunk = cubesPerAxis * cubesPerAxis * cubesPerAxis;
        int samplesPerChunk = samplesPerAxis * samplesPerAxis * samplesPerAxis;

        int cubesSize = cubesPerAxis * batchSize;
        int samplesSize = samplesPerAxis * batchSize;

        if (batchSize == 0)
            return;

        ComputeBuffer triangleSBuffer = existingBatch?.RawTriangleBuffer;
        ComputeBuffer triangleDBuffer = existingBatch?.FlatTriangleBuffer;
        ComputeBuffer triangleCBuffer = existingBatch?.TriangleChunkCounts;
        ComputeBuffer triangleCursor = existingBatch?.TriangleWriteCursor;
        ComputeBuffer detailBuffer = existingBatch?.Details;
        ComputeBuffer argsBuffer = existingBatch?.Args;
        ComputeBuffer densityBuffer = existingBatch?.DensityMap;

        ComputeBuffer dispatchArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);

        if (existingBatch == null)
        {
            triangleSBuffer = new ComputeBuffer(128000, Marshal.SizeOf<TriangleDataGPU>());
            triangleDBuffer = new ComputeBuffer(60000, Marshal.SizeOf<TriangleDataGPU>());
            triangleCBuffer = new ComputeBuffer(GenerateCap, sizeof(uint));
            triangleCursor = new ComputeBuffer(GenerateCap, sizeof(uint));
            detailBuffer = new ComputeBuffer(60000, Marshal.SizeOf<ChunkDetailDataGPU>(), ComputeBufferType.Append | ComputeBufferType.Structured);
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            densityBuffer = CreateDensityBuffer();
        }

        detailBuffer.SetCounterValue(0);

        // Fill total input.
        FillGenerateChunkInputs(keys, keyCount);

        // Density buffers.
        MarchingShader.SetBuffer(GenerateDensityMap, "DensityMap", densityBuffer);
        MarchingShader.SetBuffer(GenerateDensityMap, "ChunkInputs", GenerateChunkInputBuffer);

        // Pre-march buffers.
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "DensityMap", densityBuffer);
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "TriangleCount", triangleCBuffer);

        // Args buffers
        MarchingShader.SetBuffer(RunRepackPrePass, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(RunRepackPrePass, "TriangleCount", triangleCBuffer);
        MarchingShader.SetBuffer(RunRepackPrePass, "ArgsBuffer", argsBuffer);
        MarchingShader.SetBuffer(RunRepackPrePass, "Args1Buffer", dispatchArgs);
        MarchingShader.SetInt("BatchSize", batchSize);

        // Marching buffer
        MarchingShader.SetBuffer(RunMarchingCubes, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "DensityMap", densityBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "InitialDetailBuffer", detailBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "TriangleSourceBuffer", triangleSBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "TriangleCursor", triangleCursor);

        // Repack buffer.
        MarchingShader.SetBuffer(RunRepack, "TriangleSourceBuffer", triangleSBuffer);
        MarchingShader.SetBuffer(RunRepack, "TriangleDestBuffer", triangleDBuffer);
        MarchingShader.SetBuffer(RunRepack, "ChunkInputs", GenerateChunkInputBuffer);

        // Detail buffer
        MarchingShader.SetBuffer(RunDetailsPass, "ArgsBuffer", argsBuffer);
        MarchingShader.SetBuffer(RunDetailsPass, "DetailTriangles", triangleDBuffer);
        MarchingShader.SetBuffer(RunDetailsPass, "DetailBuffer", detailBuffer);

        int marchGroupSize = Mathf.CeilToInt(cubesPerAxis / 4f);
        int genGroupSize = Mathf.CeilToInt(samplesPerAxis / 4f);

        List<(int, int)> ranges = GroupContiguous(mods);
        foreach (var (start, end) in ranges)
        {
            int length = (end - start + 1);

            // Set the offset for the next dispatches.
            MarchingShader.SetInt("Offset", start);

            // Generate Density.
            MarchingShader.Dispatch(GenerateDensityMap, length * genGroupSize, genGroupSize, genGroupSize);

            // Clear out the modified chunk counts before the recount.
            ClearDispatch(triangleCBuffer, start, length);

            // Update triangle counts.
            MarchingShader.Dispatch(RunMarchingCubesPrePass, length * marchGroupSize, marchGroupSize, marchGroupSize);
        }

        // Update arguments (needed for triangles)
        MarchingShader.Dispatch(RunRepackPrePass, 1, 1, 1);

        foreach (var (start, end) in ranges)
        {
            int length = (end - start + 1);

            // Set the offset for the next dispatches.
            MarchingShader.SetInt("Offset", start);

            // Clear out the cursor for this range.
            ClearDispatch(triangleCursor, start, length);

            // March.
            MarchingShader.Dispatch(RunMarchingCubes, length * marchGroupSize, marchGroupSize, marchGroupSize);
        }

        MarchingShader.SetInt("Offset", 0);

        // Repack.
        MarchingShader.Dispatch(RunRepack, batchSize, 1, 1);
        MarchingShader.DispatchIndirect(RunDetailsPass, dispatchArgs);

        // Hand back a draw-ready batch (triangles + args + bounds computed from keys)
        output.Invoke(new ChunkRenderBatch(triangleSBuffer, triangleDBuffer, triangleCBuffer, triangleCursor, detailBuffer, densityBuffer, argsBuffer, this.chunkServices));

        dispatchArgs.Dispose();
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

        // Per-kernel inputs + mask output
        SurfaceChunkInputBuffer = new ComputeBuffer(SurfaceCap, Marshal.SizeOf<ChunkDispatchKeyGPU>());
        GenerateChunkInputBuffer = new ComputeBuffer(GenerateCap, Marshal.SizeOf<ChunkDispatchKeyGPU>());
        SurfaceMaskBuffer = new ComputeBuffer(SurfaceCap, sizeof(uint));

        countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ClearRange = MarchingShader.FindKernel("ClearRange");
        GenerateDensityMap = MarchingShader.FindKernel("GenerateDensityMap");
        RunMarchingCubesPrePass = MarchingShader.FindKernel("RunMarchingCubesPrePass");
        RunMarchingCubes = MarchingShader.FindKernel("RunMarchingCubes");
        RunRepackPrePass = MarchingShader.FindKernel("RunRepackPrePass");
        RunRepack = MarchingShader.FindKernel("RunRepack");
        RunDetailsPass = MarchingShader.FindKernel("RunDetailsPass");
        GenerateSurfaceMask = MarchingShader.FindKernel("GenerateSurfaceMask");

        // Set static buffers
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(RunMarchingCubesPrePass, "TriangleTableBuffer", TriangleTableBuffer);

        MarchingShader.SetBuffer(RunMarchingCubes, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(RunMarchingCubes, "TriangleTableBuffer", TriangleTableBuffer);

        // Prime options/biomes on GPU
        UpdateOptions();
    }

    private void ClearDispatch(ComputeBuffer buffer, int start, int length)
    {
        MarchingShader.SetInt("ClearStart", start);
        MarchingShader.SetInt("ClearLength", length);
        MarchingShader.SetBuffer(ClearRange, "BufferToClear", buffer);
        MarchingShader.Dispatch(ClearRange, 1, 1, 1);
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
    private void FillGenerateChunkInputs(ChunkKey?[] keys, int n)
    {
        InputGenerate.Clear();

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i].Value;
            InputGenerate.Add(new ChunkDispatchKeyGPU
            {
                GlobalIndex = (uint)i,
                CoordPos = ctx.Coordinates,
                LodIndex = ctx.LODIndex
            });
        }

        GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, n);
    }

    private ComputeBuffer CreateDensityBuffer()
    {
        // Scalar field big enough for 128 chunks at current chunk size (rough over-alloc)
        int samples = CubesPerAxis + 1 + (2 * BorderSamples);
        int sampleCountPerChunk = samples * samples * samples;
        int maxTotalSamples = sampleCountPerChunk * GenerateCap;
        return new ComputeBuffer(maxTotalSamples, sizeof(float));
    }

    /// <summary>
    /// Groups modification indices into contiguous ranges for efficient job dispatch.
    /// </summary>
    public static List<(int start, int end)> GroupContiguous(Dictionary<int, ChunkKey?> mods)
    {
        if (mods.Count == 0)
            return new List<(int, int)>();

        var sorted = mods.Keys.OrderBy(i => i);
        List<(int start, int end)> groups = new();
        int rangeStart = -1, prev = -1;

        foreach (int idx in sorted)
        {
            if (rangeStart == -1)
            {
                rangeStart = prev = idx;
                continue;
            }

            if (idx == prev + 1)
            {
                // contiguous, extend current range
                prev = idx;
            }
            else
            {
                // gap detected.
                groups.Add((rangeStart, prev));
                rangeStart = prev = idx;
            }
        }

        groups.Add((rangeStart, prev));
        return groups;
    }

    public int CubesPerAxis => densityOptions.CubesPerAxis;
    public int BorderSamples => densityOptions.BorderSamplesPerAxis;
}
