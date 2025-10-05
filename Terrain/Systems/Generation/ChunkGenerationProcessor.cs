using System.Collections.Generic;
using System;

/// <summary>
/// Coordinates the asynchronous-like generation and modification of terrain chunks.
/// Jobs are queued and processed in small batches during <see cref="Update"/> to avoid frame hitches.
/// This helps ensure high-priority chunks can be generated first if desired,
/// though the current implementation uses simple FIFO order.
/// </summary>
public class ChunkGenerationProcessor : IDisposable
{
    private const int SurfaceJobs = 1028;
    private const int GenerationJobs = 64;

    private readonly List<ChunkGenerationJob> tmpSurfaceJobs = new(SurfaceJobs);
    private readonly List<ChunkGenerationJob> tmpGenerationJobs = new(GenerationJobs);

    private readonly ChunkGenerationBatcher surfaceBatcher = new();
    private readonly ChunkGenerationBatcher generationBatcher = new();

    /// <summary>
    /// A simple queue to help with items that need to be removed.
    /// </summary>
    private List<(ChunkKey key, int framesLeft)> removalQueue = new();

    /// <summary>
    /// In earlier tests, batching surfaces takes multiple frames so this stops multiple calls. Hmm,
    /// maybe we should have one for generation if it ever takes multiple frames.
    /// </summary>
    private bool SurfaceBusy = false;

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
    public ChunkGenerationProcessor(IChunkServices services)
    {
        this.chunkServices = services;
        this.layerRenderer = new ChunkRenderRouter(services, services.Generator, 128, GenerationJobs);
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
    public void RequestChunkGeneration(ChunkKey key, Action<bool> onDone)
    {
        var job = new ChunkGenerationJob(key, onDone);
        generationBatcher.Add(job);
    }

    /// <summary>
    /// Removes all queued and active references to a given chunk.
    /// Call this when unloading or discarding a chunk to avoid processing it unnecessarily.
    /// </summary>
    public void RemoveChunk(ChunkKey key)
    {
        this.removalQueue.Add(new (key, 15));
    }

    /// <summary>
    /// Remove all chunks in the system.
    /// </summary>
    public void RemoveAll()
    {
        this.layerRenderer.Clear();
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
        UpdateRemoval();
    }

    /// <summary>
    /// Releases any GPU resources held by the render region manager.
    /// </summary>
    public void Dispose() => layerRenderer.Dispose();

    /// <summary>
    /// Loop thru the <see cref="removalQueue"/> and throw away old chunks.
    /// </summary>
    private void UpdateRemoval()
    {
        if (removalQueue.Count == 0) return;

        for (int i = removalQueue.Count - 1; i >= 0; i--)
        {
            var (key, framesLeft) = removalQueue[i];
            framesLeft--;

            if (framesLeft < 0)
            {
                surfaceBatcher.Remove(key);
                generationBatcher.Remove(key);
                layerRenderer.Remove(key);

                removalQueue.RemoveAt(i);
            }
            else
            {
                removalQueue[i] = (key, framesLeft);
            }
        }
    }

    /// <summary>
    /// Processes a batch of surface-check jobs.
    /// </summary>
    private void UpdateSurface()
    {
        if (!surfaceBatcher.HasPending) return;

        if (SurfaceBusy) 
            return;
        SurfaceBusy = true;

        int n = surfaceBatcher.TryBatch(1024, tmpSurfaceJobs);
        if (n == 0) return;

        chunkServices.Generator.DispatchSurfaceChecks(tmpSurfaceJobs, (uint[] surfaceResults) =>
        {
            for (int i = 0; i < n; i++)
            {
                bool hasSurface = surfaceResults[i] == 1;
                tmpSurfaceJobs[i].OnDone(hasSurface);
            }

            SurfaceBusy = false;
        });
    }

    /// <summary>
    /// Processes a batch of chunk generation jobs.
    /// </summary>
    private void UpdateGeneration()
    {
        if (!generationBatcher.HasPending) return;

        int n = generationBatcher.TryBatch(GenerationJobs, tmpGenerationJobs);
        if (n == 0) return;

        foreach (var job in tmpGenerationJobs)
        {
            job.OnDone(true);
            layerRenderer.Add(job);
        }
    }
}
