using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;

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
    private PriorityQueue<ChunkGenerationJob> generationQueue = new PriorityQueue<ChunkGenerationJob>(new ChunkContextComparer());
    private readonly object queueLock = new();

    /// <summary>
    /// All background workers processing the job queue.
    /// </summary>
    private List<Task?> workerTasks = new();
    private bool isProcessing = false;

    /// <summary>
    /// Signal to wake up a worker when a new job arrives.
    /// </summary>
    private readonly SemaphoreSlim jobAvailableSignal = new SemaphoreSlim(0);

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

        for (int i = 0; i < 8; i++)
        {
            workerTasks.Add(Task.Run(() => WorkerLoop(cancellationToken)));
        }
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
            int priority = context.LODIndex == 0 ? GetPriorityOfChunk(context.Coordinates) : 999;

            lock (queueLock)
            {
                generationQueue.Enqueue(newJob, priority);
            }

            jobAvailableSignal.Release();
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

        lock (queueLock)
        {
            generationQueue.Enqueue(newJob, -1); // Highest priority
        }

        jobAvailableSignal.Release();
        return newJob.Completion.Task;
    }

    /// <summary>
    /// Cancel a queued chunk generation job for the given coordinates/LOD.
    /// </summary>
    public bool CancelChunkGeneration(Vector3Int coordinates, int lodIndex)
    {
        lock (queueLock)
        {
            return generationQueue.RemoveWhere(job =>
                job.Context.Coordinates == coordinates &&
                job.Context.LODIndex == lodIndex);
        }
    }

    /// <summary>
    /// Status helper for debugging/logging.
    /// </summary>
    public override string ToString()
    {
        return $"Gen. Queue:{generationQueue.Count}\nWorkers:{workerTasks.Count}\n";
    }

    /// <summary>
    /// Core worker loop—waits for jobs and executes them.
    /// </summary>
    private async Task WorkerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await jobAvailableSignal.WaitAsync(token);

            ChunkGenerationJob? job = null;

            lock (queueLock)
            {
                job = generationQueue.Dequeue();
                if (job == null)
                    continue;
            }

            if (job.Token.IsCancellationRequested)
            {
                job.Completion.TrySetCanceled();
                continue;
            }

            try
            {
                ChunkData result = job.ModificationJob == null
                    ? WorkerNewChunk(job)
                    : WorkerModifyChunk(job);

                job.Completion.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                job.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                job.Completion.TrySetException(ex);
                Debug.LogError(ex);
            }
        }
    }

    /// <summary>
    /// Generates a brand new chunk using generator and colorizer.
    /// </summary>
    private ChunkData WorkerNewChunk(ChunkGenerationJob job)
    {
        ChunkData result = chunkServices.Generator.GenerateNewChunk(job.Context, job.Token);
        chunkServices.Colorizer.UpdateChunkColors(result, job.Context.Transform);

        return result;
    }

    /// <summary>
    /// Applies terrain brush to an existing chunk and updates its mesh.
    /// </summary>
    private ChunkData WorkerModifyChunk(ChunkGenerationJob job)
    {
        var mod = job.ModificationJob;

        chunkServices.Generator.ApplyTerrainBrush(job.Context, mod.ExistingData, mod.Brush, mod.IsAdding, job.Token);
        chunkServices.Generator.RegenerateMeshData(mod.ExistingData, job.Token);

        return mod.ExistingData;
    }

    /// <summary>
    /// Returns chunk priority based on distance to follower.
    /// Lower values = closer = higher priority.
    /// </summary>
    private int GetPriorityOfChunk(Vector3Int coordinates)
    {
        int dx = Mathf.Abs(coordinates.x - chunkServices.Layout.FollowerCoordinates.x);
        int dz = Mathf.Abs(coordinates.z - chunkServices.Layout.FollowerCoordinates.y);
        return Math.Max(dx, dz);
    }
}
