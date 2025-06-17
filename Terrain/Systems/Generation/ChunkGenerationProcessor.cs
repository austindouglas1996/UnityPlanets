using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;
using System.Linq;

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
    private ChunkGenerationBatcher batcher = new ChunkGenerationBatcher();

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
    /// Request a new chunk to be generated asynchronously.
    /// Prioritizes LOD0 chunks by proximity to the player.
    /// </summary>
    public Task<ChunkData> RequestChunkGeneration(ChunkContext context)
    {
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource(), null);

        try
        {
            this.batcher.Add(newJob);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        return newJob.Completion.Task;
    }

    /// <summary>
    /// Request a modification of an existing chunk (e.g., terrain brush).
    /// These jobs are prioritized over everything.
    /// </summary>
    public Task<ChunkData> RequestChunkModification(ChunkContext context, ChunkModificationJob modificationJob = null)
    {
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource(), modificationJob);

        batcher.Add(newJob);

        return newJob.Completion.Task;
    }

    /// <summary>
    /// Cancel a queued chunk generation job for the given coordinates/LOD.
    /// </summary>
    public bool CancelChunkGeneration(Vector3Int coordinates, int lodIndex)
    {
        return batcher.Remove(new ChunkContext(coordinates, lodIndex, this.chunkServices));
    }

    /// <summary>
    /// Update the processor and start generating chunks if there is active jobs.
    /// </summary>
    public void Update()
    {
        if (!this.batcher.HasPending) return;

        Debug.Log($"Jobs:{this.batcher.Count}");

        Dictionary<ChunkContext, ChunkGenerationJob> batch = this.batcher.TryBatch(512);

        Dictionary<Vector3Int, ChunkContext> coordToContext = new Dictionary<Vector3Int, ChunkContext>();
        foreach (var ctx in batch.Keys)
            coordToContext[ctx.Coordinates] = ctx;

        try
        {
            Dictionary<Vector3Int, MeshData> results = chunkServices.Generator.DispatchGeneration(batch.Keys.ToList(), this.cancellationToken);
            HashSet<ChunkContext> completed = new HashSet<ChunkContext>();

            foreach (var kvp in results)
            {
                if (coordToContext.TryGetValue(kvp.Key, out var context) && batch.TryGetValue(context, out var job))
                {
                    job.Completion.TrySetResult(new ChunkData(context, kvp.Value));
                    completed.Add(context);
                }
            }

            // Cancel any jobs that were not resolved
            foreach (var kvp in batch)
            {
                if (!completed.Contains(kvp.Key))
                    kvp.Value.Completion.TrySetCanceled();
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
