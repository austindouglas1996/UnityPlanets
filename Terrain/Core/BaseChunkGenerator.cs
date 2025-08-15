using System.Collections.Generic;

/// <summary>
/// Handles generating chunk meshes and density maps for marching cubes.
/// </summary>
public abstract class BaseChunkGenerator : IChunkGenerator
{
    private IChunkConfiguration configuration;
    public BaseChunkGenerator(IChunkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    protected IChunkConfiguration Configuration
    {
        get { return configuration; }
    }

    public abstract MarchingCubesGPUDispatcher Generator { get; }

    /// <summary>
    /// Generates a new chunk from coordinates using the provided configuration.
    /// </summary>
    /// <param name="coordinates">The chunk coordinates in the world.</param>
    /// <param name="config">The chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>The generated chunk data.</returns>
    public virtual ChunkRenderBatch DispatchGeneration(IReadOnlyList<ChunkKey> chunkContexts)
    {
        return this.Generator.DispatchGeneration(chunkContexts);
    }

    public void Dispose()
    {
        this.Generator.Dispose();
    }

    public uint[] DispatchSurface(IReadOnlyList<ChunkGenerationJob> jobs)
    {
        return this.Generator.GetSurfaceMask(jobs);
    }
}