using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.VisualScripting;
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
    //private ComputeBuffer TriangleCountBuffer;

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

    uint[] zeroCounts = new uint[GenerateCap];

    // Kernel ID's.
    private int surfaceKernel;
    private int genKernel;
    private int preMarchKernel;
    private int marchKernel;
    private int detailKernel;
    private int argsKernel;

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
        MarchingShader.SetBuffer(surfaceKernel, "ChunkInputs", SurfaceChunkInputBuffer);
        MarchingShader.SetBuffer(surfaceKernel, "SurfaceMask", SurfaceMaskBuffer);

        MarchingShader.Dispatch(surfaceKernel, batchSize, 1, 1);

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
    private void ProcessBatch(ChunkKey?[] keys, int keyCount, Dictionary<int,ChunkKey?> mods, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
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

        ComputeBuffer triangleBuffer = existingBatch?.Triangle;
        ComputeBuffer detailBuffer = existingBatch?.Details;
        ComputeBuffer argsBuffer = existingBatch?.Args;
        ComputeBuffer densityBuffer = existingBatch?.DensityMap;

        if (existingBatch == null)
        {
            triangleBuffer = new ComputeBuffer(60000, Marshal.SizeOf<TriangleDataGPU>());
            detailBuffer = new ComputeBuffer(60000, Marshal.SizeOf<ChunkDetailDataGPU>(), ComputeBufferType.Append | ComputeBufferType.Structured);
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            densityBuffer = CreateDensityBuffer();
        }

        detailBuffer.SetCounterValue(0);

        // Fill total input.
        FillGenerateChunkInputs(keys, keyCount);

        MarchingShader.SetBuffer(genKernel, "DensityMap", densityBuffer);
        MarchingShader.SetBuffer(genKernel, "ChunkInputs", GenerateChunkInputBuffer);

        List<(int, int)> ranges = GroupContiguous(mods);
        foreach (var (start, end) in ranges)
        {
            // Generate density
            MarchingShader.SetInt("Offset", start);

            // NOTE: thread group dims assume [numthreads(4,4,4)] and X packs chunkIndex*XWithinChunk
            int genGroupSize = Mathf.CeilToInt(samplesPerAxis / 4f);
            MarchingShader.Dispatch(genKernel, (end - start + 1) * genGroupSize, genGroupSize, genGroupSize);
        }

        int marchGroupSize = Mathf.CeilToInt(cubesPerAxis / 4f);

        var TriangleCountBuffer = new ComputeBuffer(GenerateCap, sizeof(uint));
        TriangleCountBuffer.SetData(zeroCounts);

        // Pre-marching.
        MarchingShader.SetBuffer(preMarchKernel, "ChunkInputs", GenerateChunkInputBuffer);
        MarchingShader.SetBuffer(preMarchKernel, "DensityMap", densityBuffer);
        MarchingShader.SetBuffer(preMarchKernel, "TriangleCount", TriangleCountBuffer);
        MarchingShader.Dispatch(preMarchKernel, batchSize * marchGroupSize, marchGroupSize, marchGroupSize);

        AsyncGPUReadback.Request(TriangleCountBuffer, (req) =>
        {
            if (req.hasError)
            {
                Debug.LogError("TriangleCountBuffer readback failed.");
                return;
            }

            // Fill with the new modified data.
            var triCount = req.GetData<uint>();
            int triangles = FillGenerateChunkInputs(keys, keyCount, triCount);

            // Marching cubes
            MarchingShader.SetBuffer(marchKernel, "ChunkInputs", GenerateChunkInputBuffer);
            MarchingShader.SetBuffer(marchKernel, "DensityMap", densityBuffer);
            MarchingShader.SetBuffer(marchKernel, "InitialDetailBuffer", detailBuffer);
            MarchingShader.SetBuffer(marchKernel, "TriangleBuffer", triangleBuffer);
            MarchingShader.SetBuffer(marchKernel, "TriangleCount", TriangleCountBuffer);

            MarchingShader.Dispatch(marchKernel, batchSize * marchGroupSize, marchGroupSize, marchGroupSize);

            // Build indirect args from append count
            MarchingShader.SetInt("Triangles", triangles);
            MarchingShader.SetBuffer(argsKernel, "ArgsBuffer", argsBuffer);
            MarchingShader.Dispatch(argsKernel, 1, 1, 1);

            MarchingShader.SetBuffer(detailKernel, "Biomes", BiomeBuffer);
            MarchingShader.SetInt("_BiomesCount", BiomesCount);
            MarchingShader.SetBuffer(detailKernel, "ArgsBuffer", argsBuffer);
            MarchingShader.SetBuffer(detailKernel, "DetailTriangles", triangleBuffer);
            MarchingShader.SetBuffer(detailKernel, "DetailBuffer", detailBuffer);

            int groupsX = Mathf.CeilToInt(triangles / 64f);
            MarchingShader.Dispatch(detailKernel, groupsX, 1, 1);

            /*
                uint[] args = new uint[5];
                argsBuffer.GetData(args);
                uint triCount = args[0] / 3;
                Debug.Log($"LOD{keys[0].LODIndex} - Chunks {batchSize} - Triangles written: {triCount}, triangle capacity of buffer {triangleBuffer.count}");
            */

            // Hand back a draw-ready batch (triangles + args + bounds computed from keys)
            output.Invoke(new ChunkRenderBatch(triangleBuffer, triangles, detailBuffer, densityBuffer, argsBuffer, this.chunkServices));
        });
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

        genKernel = MarchingShader.FindKernel("GenerateDensityMap");
        preMarchKernel = MarchingShader.FindKernel("RunMarchingCubesPrePass");
        marchKernel = MarchingShader.FindKernel("RunMarchingCubes");
        detailKernel = MarchingShader.FindKernel("RunDetailsPass");
        argsKernel = MarchingShader.FindKernel("PrepareDrawArgs");
        surfaceKernel = MarchingShader.FindKernel("GenerateSurfaceMask");

        // Set static buffers
        MarchingShader.SetBuffer(preMarchKernel, "CornerOffsetsBuffer", CornerOffsetsBuffer);
        MarchingShader.SetBuffer(preMarchKernel, "EdgeConnectionsBuffer", EdgeConnectionsBuffer);
        MarchingShader.SetBuffer(preMarchKernel, "TriangleTableBuffer", TriangleTableBuffer);

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

    private int FillGenerateChunkInputs(ChunkKey?[] keys, int n, NativeArray<uint> triCount)
    {
        InputGenerate.Clear();
        uint baseIndex = 0;

        for (int i = 0; i < n; i++)
        {
            var ctx = keys[i].Value;
            InputGenerate.Add(new ChunkDispatchKeyGPU
            {
                GlobalIndex = (uint)i,
                CoordPos = ctx.Coordinates,
                LodIndex = ctx.LODIndex,
                TriangleStart = baseIndex
            });

            baseIndex += triCount[i];

            Debug.Log($"Triangles: {triCount[i]}, Start {baseIndex}");
        }

        GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, n);

        return (int)baseIndex;
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
