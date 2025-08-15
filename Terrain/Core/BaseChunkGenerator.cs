using System.Collections.Generic;

/// <summary>
/// Abstract base for terrain chunk generators that produce marching-cubes outputs.
/// Wraps a <see cref="ITerrainGenerator"/> and forwards generation/surface queries.
/// </summary>
/// <remarks>
/// - Call from the main Unity thread (buffers/materials aren’t thread-safe).
/// - This class does not create Unity GameObjects; it only returns GPU draw data.
/// - Derive from this to swap dispatchers or inject preprocessing/postprocessing.
/// </remarks>
public abstract class BaseChunkGenerator : IChunkGenerator
{
    private IChunkConfiguration configuration;

    /// <summary>
    /// Construct a generator base bound to a shared chunk configuration.
    /// </summary>
    /// <param name="configuration">Static/runtime settings that inform generation.</param>
    public BaseChunkGenerator(IChunkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <summary>
    /// The active configuration used by derived classes during generation.
    /// </summary>
    protected IChunkConfiguration Configuration
    {
        get { return configuration; }
    }

    /// <summary>
    /// The GPU dispatcher that performs density/meshing work.
    /// </summary>
    public abstract ITerrainGenerator Generator { get; }

    /// <summary>
    /// Generate a draw-ready batch (triangle buffer + indirect args + bounds)
    /// for the given set of chunk keys. Forwards to <see cref="Generator"/>.
    /// </summary>
    /// <param name="keys">Non-empty list of chunk keys to build.</param>
    /// <returns>A <see cref="ChunkRenderBatch"/> ready to render, or throws if empty.</returns>
    public virtual ChunkRenderBatch DispatchGeneration(IReadOnlyList<ChunkKey> keys)
    {
        return this.Generator.GenerateBatch(keys);
    }

    /// <summary>
    /// Compute a per-chunk surface mask to quickly cull empty chunks before meshing.
    /// Forwards to <see cref="Generator"/>.
    /// </summary>
    /// <param name="jobs">Chunk jobs describing coordinates/LOD for masking.</param>
    /// <returns>
    /// One <c>uint</c> per job containing the bitmask/word produced by the mask kernel.
    /// </returns>
    public uint[] DispatchSurfaceChecks(IReadOnlyList<ChunkGenerationJob> jobs)
    {
        return this.Generator.GetSurfaceMaskChecks(jobs);
    }

    /// <summary>
    /// Release GPU resources owned by the underlying dispatcher.
    /// Safe to call during teardown.
    /// </summary>
    public void Dispose()
    {
        this.Generator.Dispose();
    }

    /// <summary>
    /// Apply updated runtime/editor options to the generator (e.g., density params, biome tables).
    /// Implementations should re-upload constant/structured buffers and invalidate any caches
    /// so subsequent builds reflect the new settings.
    /// </summary>
    public void UpdateOptions()
    {
        this.Generator.UpdateOptions();
    }
}
