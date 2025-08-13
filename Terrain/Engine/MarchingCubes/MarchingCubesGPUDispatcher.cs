using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
public class MarchingCubesGPUDispatcher
{
    private IChunkServices services;
    private IChunkConfiguration configuration;

    private ComputeShader GenerateShader;
    private ComputeShader MarchingShader;

    /// <summary>
    /// Creates a new marching cube generator with the given density options.
    /// </summary>
    /// <param name="options">The configuration used for density generation and surface thresholds.</param>
    public MarchingCubesGPUDispatcher(IChunkServices services, IChunkConfiguration configuration, DensityMapOptions options)
    {
        this.services = services;

        this.GenerateShader = services.ChunkManager.GenerateDensity;
        this.MarchingShader = services.ChunkManager.MarchingCubes;

        this.configuration = configuration;
        this.Options = options;

        this.InitBuffer();
    }

    /// <summary>
    /// The options used for generating density and controlling surface behavior.
    /// </summary>
    public DensityMapOptions Options { get; set; }

    private ComputeBuffer DensityBuffer;
    private ComputeBuffer ChunkInputBuffer;
    private ComputeBuffer Chunk1InputBuffer;
    private ComputeBuffer BiomeBuffer;
    private ComputeBuffer DensityOptionsBuffer;
    private ComputeBuffer SurfaceMaskBuffer;
    private int BiomeCount = 0;

    public void Dispose()
    {
        DensityBuffer.Dispose();
        BiomeBuffer.Dispose();
        DensityOptionsBuffer.Dispose();
        ChunkInputBuffer.Dispose();
        Chunk1InputBuffer.Dispose();
        SurfaceMaskBuffer.Dispose();
    }

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

        DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<DensityMapOptions>());
        DensityOptionsBuffer.SetData(new[] { Options });

        int size = Options.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int maxTotalVoxels = voxelCountPerChunk * 128;

        DensityBuffer = new ComputeBuffer(maxTotalVoxels, sizeof(float));
        ChunkInputBuffer = new ComputeBuffer(128, Marshal.SizeOf<ChunkInput>());
        ChunkInputBuffer = new ComputeBuffer(1028, Marshal.SizeOf<ChunkInput>());

        SurfaceMaskBuffer = new ComputeBuffer(1028, sizeof(uint));
    }

    public uint[] GetSurfaceMask(IReadOnlyList<ChunkGenerationJob> chunkContexts)
    {
        int batchSize = chunkContexts.Count;
        var chunkInputs = new List<ChunkInput>(batchSize);
        for (int i = 0; i < batchSize; i++)
        {
            var ctx = chunkContexts[i];
            chunkInputs.Add(new ChunkInput
            {
                CoordPos = ctx.Key.Coordinates,
                WorldPos = services.Layout.ToWorld(ctx.Key),
                stepSize = 1 << ctx.Key.LODIndex
            });
        }

        ChunkInputBuffer.SetData(chunkInputs, 0, 0, batchSize);

        GenerateShader.SetInt("_ChunkInputCount", batchSize);
        GenerateShader.SetBuffer(1, "ChunkInputs", ChunkInputBuffer);
        GenerateShader.SetBuffer(1, "DensityOptions", DensityOptionsBuffer);
        GenerateShader.SetBuffer(1, "SurfaceMask", SurfaceMaskBuffer);
        GenerateShader.Dispatch(1, Mathf.CeilToInt(batchSize / 64f), 1, 1);

        uint[] surfaceWords = new uint[batchSize];
        SurfaceMaskBuffer.GetData(surfaceWords);

        return surfaceWords;
    }

    public virtual GPUSet DispatchGeneration(IReadOnlyList<ChunkKey> chunkContexts)
    {
        if (chunkContexts.Count == 0)
            throw new System.ArgumentException("Tried to dispatch...0 contexts?");

        int batchSize = chunkContexts.Count;
        int size = Options.ChunkSize + 1;
        int voxelCountPerChunk = size * size * size;
        int totalVoxels = voxelCountPerChunk * batchSize;

        var chunkInputs = new List<ChunkInput>(batchSize);
        for (int i = 0; i < batchSize; i++)
        {
            var ctx = chunkContexts[i];
            chunkInputs.Add(new ChunkInput
            {
                CoordPos = ctx.Coordinates,
                WorldPos = services.Layout.ToWorld(ctx),
                stepSize = 1 << ctx.LODIndex
            });
        }

        ChunkInputBuffer.SetData(chunkInputs,0,0,batchSize);

        ComputeBuffer triangleBuffer = new ComputeBuffer(totalVoxels, Marshal.SizeOf<Triangle>(), ComputeBufferType.Append);
        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        triangleBuffer.SetCounterValue(0);

        GenerateShader.SetBuffer(0, "ChunkInputs", ChunkInputBuffer);
        GenerateShader.SetBuffer(0, "DensityMap", DensityBuffer);
        GenerateShader.SetBuffer(0, "DensityOptions", DensityOptionsBuffer);
        GenerateShader.Dispatch(0, Mathf.CeilToInt(batchSize * size / 8f), Mathf.CeilToInt(size / 8f), Mathf.CeilToInt(size / 8f));

        MarchingShader.SetBuffer(0, "DensityMap", DensityBuffer);
        MarchingShader.SetBuffer(0, "TriangleBuffer", triangleBuffer);
        MarchingShader.SetBuffer(0, "ChunkInputs", ChunkInputBuffer);
        MarchingShader.SetBuffer(0, "DensityOptions", DensityOptionsBuffer);
        MarchingShader.SetBuffer(0, "BiomeColors", BiomeBuffer);
        MarchingShader.SetInt("_BiomeCount", BiomeCount);
        MarchingShader.Dispatch(0, batchSize * 2,2,2);

        ComputeBuffer.CopyCount(triangleBuffer, argsBuffer, 0);

        MarchingShader.SetBuffer(1, "ArgsBuffer", argsBuffer);
        MarchingShader.Dispatch(1, 1, 1, 1);

        return new GPUSet(triangleBuffer, argsBuffer, chunkContexts.ToList(), services);
    }
}