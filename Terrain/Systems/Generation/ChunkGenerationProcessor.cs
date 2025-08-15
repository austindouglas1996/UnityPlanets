using System.Collections.Generic;
using System;

using UnityEngine;

/// <summary>
/// Coordinates the asynchronous-like generation and modification of terrain chunks.
/// Jobs are queued and processed in small batches during <see cref="Update"/> to avoid frame hitches.
/// This helps ensure high-priority chunks can be generated first if desired,
/// though the current implementation uses simple FIFO order.
/// </summary>
public class ChunkGenerationProcessor : IDisposable
{
    private readonly List<ChunkGenerationJob> tmpSurfaceJobs = new(1024);
    private readonly List<ChunkGenerationJob> tmpGenerationJobs = new(1024);

    private readonly ChunkGenerationBatcher surfaceBatcher = new();
    private readonly ChunkGenerationBatcher generationBatcher = new();

    /// <summary>
    /// Manages GPU-side render regions for generated chunks.
    /// </summary>
    private readonly ChunkRenderRouter layerRenderer;

    /// <summary>
    /// Core services used for chunk generation (density generator, colorizer, layout, etc.).
    /// </summary>
    private readonly IChunkServices chunkServices;

    /// <summary>
    /// Creates a new generation processor.
    /// Initializes the GPU render region manager and sets up default material parameters.
    /// </summary>
    public ChunkGenerationProcessor(IChunkServices services, ChunkRenderRouter layerRenderer)
    {
        this.chunkServices = services;
        this.layerRenderer = layerRenderer;
    }

    /// <summary>
    /// Queues a chunk to be checked for surface data before full generation.
    /// The provided callback will be invoked once the check completes.
    /// </summary>
    public void RequestSurfaceCheck(ChunkKey key, Action<bool> onDone) =>
        surfaceBatcher.Add(new ChunkGenerationJob(key, onDone));

    /// <summary>
    /// Queues a chunk for full generation.
    /// The provided callback will be invoked once generation is complete.
    /// </summary>
    public void RequestChunkGeneration(ChunkKey key, Action<bool> onDone) =>
        generationBatcher.Add(new ChunkGenerationJob(key, onDone));

    /// <summary>
    /// Removes all queued and active references to a given chunk.
    /// Call this when unloading or discarding a chunk to avoid processing it unnecessarily.
    /// </summary>
    public void RemoveChunk(ChunkKey key)
    {
        surfaceBatcher.Remove(key);
        generationBatcher.Remove(key);
        layerRenderer.Remove(key);
    }

    /// <summary>
    /// Issues draw calls for any currently active render regions.
    /// Should be called from the main rendering loop.
    /// </summary>
    public void Draw() => layerRenderer.Draw();

    /// <summary>
    /// Processes queued surface and generation jobs in small batches,
    /// and updates the GPU render regions.
    /// Call this once per frame.
    /// </summary>
    public void Update()
    {
        this.layerRenderer.Update();

        UpdateSurface();
        UpdateGeneration();
    }

    /// <summary>
    /// Releases any GPU resources held by the render region manager.
    /// </summary>
    public void Dispose() => layerRenderer.Dispose();

    /// <summary>
    /// Processes a batch of surface-check jobs.
    /// </summary>
    private void UpdateSurface()
    {
        if (!surfaceBatcher.HasPending) return;

        int n = surfaceBatcher.TryBatch(1024, tmpSurfaceJobs);
        if (n == 0) return;

        var surfaceResults = chunkServices.Generator.DispatchSurfaceChecks(tmpSurfaceJobs);

        for (int i = 0; i < n; i++)
            tmpSurfaceJobs[i].OnDone(surfaceResults[i] == 1);
    }

    /// <summary>
    /// Processes a batch of chunk generation jobs.
    /// </summary>
    private void UpdateGeneration()
    {
        if (!generationBatcher.HasPending) return;

        int n = generationBatcher.TryBatch(64, tmpGenerationJobs);
        if (n == 0) return;

        foreach (var job in tmpGenerationJobs)
        {
            job.OnDone(true);
            layerRenderer.Add(job.Key);
        }
    }
}
