using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

/// <summary>
/// Handles the asynchronous generation and modification of terrain chunks.
/// Uses a priority queue to ensure high-importance chunks (near the player) are processed first.
/// Spawns multiple worker threads that continuously process jobs in the background.
/// </summary>
public class ChunkGenerationProcessor
{
    /// <summary>
    /// A priority queue holding pending generation jobs.
    /// LOD0 jobs are prioritized higher for near-player chunks.
    /// </summary>
    private ChunkGenerationBatcher surfaceBatch = new ChunkGenerationBatcher();
    private ChunkGenerationBatcher generationBatch = new ChunkGenerationBatcher();

    /// <summary>
    /// A system to help with controlling render regions with the GPU. We are unable
    /// to modify buffers.
    /// </summary>
    private ChunkRenderRegionManager regionManager;

    /// <summary>
    /// Central services used during chunk generation (generator, colorizer, layout, etc.).
    /// </summary>
    private IChunkServices chunkServices;

    /// <summary>
    /// Construct a new generation processor, spin up a few worker threads.
    /// </summary>
    public ChunkGenerationProcessor(IChunkServices services, CancellationToken token)
    {
        this.chunkServices = services;
        this.cancellationToken = token;

        Material mat = new Material(Shader.Find("Custom/URP_CustomLitGPU"));
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_UseVertexColor", 1f);

        this.regionManager = new ChunkRenderRegionManager(chunkServices.Generator, mat);
    }

    /// <summary>
    /// Token to cancel all processing (e.g., game closing).
    /// </summary>
    public CancellationToken CancellationToken
    {
        get => cancellationToken;
        set => cancellationToken = value;
    }
    private CancellationToken cancellationToken;

    /// <summary>
    /// Request a new chunk to be checked for surface data before generation.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public Task RequestSurfaceCheck(ChunkContext context)
    {
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource());

        try
        {
            this.surfaceBatch.Add(newJob);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        return newJob.Completion.Task;
    }

    /// <summary>
    /// Request a new chunk to be generated asynchronously.
    /// Prioritizes LOD0 chunks by proximity to the player.
    /// </summary>
    public Task RequestChunkGeneration(ChunkContext context)
    {
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource());

        try
        {
            this.generationBatch.Add(newJob);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        return newJob.Completion.Task;
    }

    public void RemoveChunk(ChunkContext context)
    {
        this.surfaceBatch.Remove(context);
        this.generationBatch.Remove(context);
        this.regionManager.Remove(context);
    }

    /// <summary>
    /// Cancel a queued chunk generation job for the given coordinates/LOD.
    /// </summary>
    public bool CancelChunkGeneration(Vector3Int coordinates, int lodIndex)
    {
        var ctx = new ChunkContext(coordinates, lodIndex, this.chunkServices);

        this.surfaceBatch.Remove(ctx);
        this.generationBatch.Remove(ctx);

        return true;
    }

    public void Dipose()
    {
        this.regionManager.Dispose();
    }

    public void Draw()
    {
        this.regionManager.Draw();
    }

    /// <summary>
    /// Update the processor and start generating chunks if there is active jobs.
    /// </summary>
    private int surfaceUpdateFrameCounter = 0;

    public void Update()
    {
        regionManager.Update(cancellationToken);

        this.UpdateSurface();
        this.UpdateGeneration();
    }


    private int cancelled = 0;
    private int total = 0;

    private void UpdateSurface()
    {
        if (!this.surfaceBatch.HasPending)
            return;

        Dictionary<ChunkContext, ChunkGenerationJob> batch = this.surfaceBatch.TryBatch(1028);

        var surfaceChunks = this.chunkServices.Generator.DispatchSurface(batch.Keys.ToList());

        int index = 0;

        foreach (var ctx in batch.Keys.ToList()) // Avoid modifying while iterating
        {
            if (batch.TryGetValue(ctx, out var job))
            {
                if (surfaceChunks[index] == 0)
                {
                    job.Completion.TrySetCanceled();
                    cancelled++;
                }
                else
                {
                    job.Completion.TrySetResult(ctx);
                }

                total++;
            }

            index++;
        }

        Debug.Log($"Cancelled: {cancelled} / out of {total}");
    }

    private void UpdateGeneration()
    {
        if (!this.generationBatch.HasPending || this.generationBatch.Count < 100)
            return;

        Dictionary<ChunkContext, ChunkGenerationJob> batch = this.generationBatch.TryBatch(128);

        try
        {
            foreach (var job in batch.Values)
            {
                regionManager.Add(job.Context);
                job.Completion.TrySetResult(job.Context);
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var job in batch.Values)
                job.Completion.TrySetCanceled();
        }
        catch (Exception ex)
        {
            foreach (var job in batch.Values)
                job.Completion.TrySetException(ex);
            Debug.LogError(ex);
        }
    }
}
