using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract base for terrain chunk generators that produce marching-cubes outputs.
/// Wraps a <see cref="ITerrainGenerator"/> and forwards generation/surface queries.
/// </summary>
/// <remarks>
/// - Call from the main Unity thread (buffers/materials aren’t thread-safe).
/// - This class does not create Unity GameObjects; it only returns GPU draw data.
/// - Derive from this to swap dispatchers or inject preprocessing/postprocessing.
/// </remarks>
public abstract class BaseChunkGenerator : BaseChunkCore, IChunkGenerator
{
    /// <summary>
    /// Construct a generator base bound to a shared chunk configuration.
    /// </summary>
    /// <param name="configuration">Static/runtime settings that inform generation.</param>
    public BaseChunkGenerator(IChunkConfiguration configuration)
        : base(configuration)
    {
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
    public virtual void DispatchGeneration(IReadOnlyList<ChunkKey> keys, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null)
    {
        this.Generator.GenerateBatch(keys, output, existingBatch);
    }

    /// <summary>
    /// Compute a per-chunk surface mask to quickly cull empty chunks before meshing.
    /// Forwards to <see cref="Generator"/>.
    /// </summary>
    /// <param name="jobs">Chunk jobs describing coordinates/LOD for masking.</param>
    /// <returns>
    /// One <c>uint</c> per job containing the bitmask/word produced by the mask kernel.
    /// </returns>
    public void DispatchSurfaceChecks(IReadOnlyList<ChunkGenerationJob> jobs, Action<uint[]> output)
    {
        this.Generator.GetSurfaceMaskChecks(jobs, output);
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
    /// Used for generators that operate on a schedule.
    /// </summary>
    public void Update()
    {
        this.Generator.Update();
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

    /// <summary>
    /// Get the custom material used in generation.
    /// </summary>
    public Material GetMaterial => this.Generator.GetMaterial;
}
