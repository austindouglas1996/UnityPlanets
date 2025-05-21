using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine;
using System.Threading;
using System.Collections.ObjectModel;
using NUnit.Framework.Interfaces;
using System.Text;

public class ChunkGenerationQueue
{
    /// <summary>
    /// A dictionary of active chunks with their jobs. Allows for one respective job, but also
    /// works with LOD so past jobs are cancelled before being queued. 
    /// </summary>
    private readonly Dictionary<Vector3Int, ChunkGenerationJob> pendingJobs = new Dictionary<Vector3Int, ChunkGenerationJob>();
    private readonly object pendingJobsLock = new();

    /// <summary>
    /// A collection of jobs to be executed yet. Seperate from active jobs, this runs the actual
    /// task.
    /// </summary>
    private PriorityQueue<ChunkGenerationJob> generationQueue = new PriorityQueue<ChunkGenerationJob>();
    private readonly object queueLock = new();

    /// <summary>
    /// A collection of tasks to run the process queue.
    /// </summary>
    private List<Task?> workerTasks = new();
    private bool isProcessing = false;

    /// <summary>
    /// A collection of services to help with chunk generation.
    /// </summary>
    private IChunkServices chunkServices;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkGenerationQueue"/> class.
    /// </summary>
    public ChunkGenerationQueue(IChunkServices services, CancellationToken token)
    {
        this.chunkServices = services;
        this.cancellationToken = token;

        for (int i = 0; i < 8; i++)
        {
            workerTasks.Add(Task.Run(() => WorkerLoop(cancellationToken)));
        }
    }

    /// <summary>
    /// A cancellation token to help with cancelling all jobs when the game is closed.
    /// </summary>
    public CancellationToken CancellationToken
    {
        get { return cancellationToken; }
        set { cancellationToken = value; }
    }
    private CancellationToken cancellationToken;

    /// <summary>
    /// Request chunk generation for a given chunk. Given LOD, certain details are required to generate an appropiate job.
    /// The job will then be tracked and cancelled if another job from the same coordinates is given. 
    /// </summary>
    /// <param name="coordinates"></param>
    /// <param name="LODIndex"></param>
    /// <param name="generationTask"></param>
    /// <returns></returns>
    public Task<ChunkData> RequestChunkGeneration(Vector3Int coordinates, int LODIndex)
    {
        lock (queueLock) 
        {
            // Try to find an existing job. If for some reason we 
            // have a job of the same LOD, return it. If not
            // LOD must have changed so cancel active.
            if (pendingJobs.TryGetValue(coordinates, out var job))
            {
                if (job.LODIndex == LODIndex)
                    return job.Completion.Task;

                job.Cancel();
                pendingJobs.Remove(coordinates);
            }

            ChunkGenerationJob newJob = new(coordinates, LODIndex, new CancellationTokenSource(), null);

            try
            {
                // Register job as active
                pendingJobs[coordinates] = newJob;
                generationQueue.Enqueue(newJob, LODIndex == 0 ? GetPriorityOfChunk(coordinates) : 999);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }

            return newJob.Completion.Task;
        }
    }

    /// <summary>
    /// Request a chunk generation for a given existing chunk. This chunk should be modified and given the highest of importance on updates.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <param name="LODIndex"></param>
    /// <param name="modificationJob"></param>
    /// <returns></returns>
    public Task<ChunkData> RequestChunkModification(Vector3Int coordinates, int LODIndex, ChunkModificationJob modificationJob = null)
    {
        lock (queueLock)
        {
            ChunkGenerationJob newJob = new(coordinates, LODIndex, new CancellationTokenSource(), modificationJob);

            generationQueue.Enqueue(newJob, -1);

            return newJob.Completion.Task;
        }
    }

    /// <summary>
    /// Cancel a chunk generation task if one exists.
    /// </summary>
    /// <param name="coordinates"></param>
    public void CancelChunkGeneration(Vector3Int coordinates)
    {
        lock (queueLock)
        {
            if (pendingJobs.TryGetValue(coordinates, out var job))
            {
                job.Cancel();
                pendingJobs.Remove(coordinates);
            }
        }
    }

    /// <summary>
    /// Provide a breif status on what is going in the system.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"Pending:{pendingJobs.Count}\nGen. Queue:{generationQueue.Count}\nWorkers:{workerTasks.Count}\n";
    }

    /// <summary>
    /// A loop to go through each job and render the desired chunk.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task WorkerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ChunkGenerationJob? job = null;

            lock (queueLock)
            {
                if (generationQueue.Count > 0)
                    job = generationQueue.Dequeue();
            }

            if (job == null)
            {
                await Task.Delay(10, token); // Slight pause before polling again
                continue;
            }

            if (job.Token.IsCancellationRequested)
            {
                lock (queueLock)
                {
                    job.Completion.TrySetCanceled();
                    pendingJobs.Remove(job.Coordinates);
                    continue;
                }
            }

            try
            {
                ChunkData result = job.ModificationJob == null ?
                    WorkerNewChunk(job) : WorkerModifyChunk(job);

                job.Completion.TrySetResult(result);

                lock (queueLock)
                {
                    pendingJobs.Remove(job.Coordinates);
                }
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
    /// Generate a new chunk given a <see cref="ChunkGenerationJob"/>.
    /// </summary>
    /// <param name="job"></param>
    /// <returns></returns>
    private ChunkData WorkerNewChunk(ChunkGenerationJob job)
    {
        ChunkData result = chunkServices.Generator.GenerateNewChunk(job.Coordinates, job.LODIndex, job.Token);

        Vector3 worldPos = chunkServices.Layout.ToWorld(job.Coordinates, job.LODIndex);

        Matrix4x4 transform = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one);
        chunkServices.Colorizer.UpdateChunkColors(result, transform);

        return result;
    }

    /// <summary>
    /// Generate a chunk with modified values given a <see cref="ChunkGenerationJob"/>.
    /// </summary>
    /// <param name="job"></param>
    /// <returns></returns>
    private ChunkData WorkerModifyChunk(ChunkGenerationJob job)
    {
        ChunkModificationJob mod = job.ModificationJob;

        chunkServices.Generator.ApplyTerrainBrush(mod.ExistingData, mod.Brush, job.Coordinates, mod.IsAdding, job.Token);
        chunkServices.Generator.RegenerateMeshData(mod.ExistingData, job.Token);

        // We set the original data back.
        return mod.ExistingData;
    }

    /// <summary>
    /// Return the priority of this chunk based on the distance from the follower.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    private int GetPriorityOfChunk(Vector3Int coordinates)
    {
        int dx = Mathf.Abs(coordinates.x - this.chunkServices.Layout.FollowerCoordinates.x);
        int dz = Mathf.Abs(coordinates.z - this.chunkServices.Layout.FollowerCoordinates.y);

        return Math.Max(dx, dz);
    }
}
