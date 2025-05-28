using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine;
using System.Threading;
using System.Collections.ObjectModel;
using NUnit.Framework.Interfaces;
using System.Text;
using UnityEngine.InputSystem;

public class ChunkGenerationProcessor
{
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
    /// Initialize a new instance of the <see cref="ChunkGenerationProcessor"/> class.
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
    public Task<ChunkData> RequestChunkGeneration(ChunkContext context)
    {
        var key = new ChunkJobKey(context.Coordinates, context.LODIndex);
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource(), null);

        try
        {
            lock (queueLock)
            {
                generationQueue.Enqueue(newJob, context.LODIndex == 0 ? GetPriorityOfChunk(context.Coordinates) : 999);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        return newJob.Completion.Task;
    }

    /// <summary>
    /// Request a chunk generation for a given existing chunk. This chunk should be modified and given the highest of importance on updates.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <param name="LODIndex"></param>
    /// <param name="modificationJob"></param>
    /// <returns></returns>
    public Task<ChunkData> RequestChunkModification(ChunkContext context, ChunkModificationJob modificationJob = null)
    {
        ChunkGenerationJob newJob = new(context, new CancellationTokenSource(), modificationJob);

        lock (queueLock)
        {
            generationQueue.Enqueue(newJob, -1);
        }

        return newJob.Completion.Task;
    }

    /// <summary>
    /// Cancel a chunk generation task if one exists.
    /// </summary>
    /// <param name="coordinates"></param>
    public bool CancelChunkGeneration(Vector3Int coordinates, int lodIndex)
    {
        lock (queueLock)
        {
            // This is nasty and should probably not be done like this, but it works?
            // ChunkGenerationJob has an IEqualityComparer to only compare coordinates/LOD.
            return this.generationQueue.Remove(new ChunkGenerationJob(new ChunkContext(coordinates, lodIndex, null), null, null));
        }
    }

    /// <summary>
    /// Provide a breif status on what is going in the system.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"Gen. Queue:{generationQueue.Count}\nWorkers:{workerTasks.Count}\n";
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
                {
                    job = generationQueue.Dequeue();
                    if (job.Token.IsCancellationRequested)
                    {
                        job.Completion.TrySetCanceled();
                        continue;
                    }
                }
            }

            if (job == null)
            {
                await Task.Delay(1, token);
                continue;
            }

            try
            {
                ChunkData result = job.ModificationJob == null ?
                    WorkerNewChunk(job) : WorkerModifyChunk(job);
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
    /// Generate a new chunk given a <see cref="ChunkGenerationJob"/>.
    /// </summary>
    /// <param name="job"></param>
    /// <returns></returns>
    private ChunkData WorkerNewChunk(ChunkGenerationJob job)
    {
        ChunkData result = chunkServices.Generator.GenerateNewChunk(job.Context, job.Token);

        Matrix4x4 transform = Matrix4x4.TRS(job.Context.WorldPosition, Quaternion.identity, Vector3.one);
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

        chunkServices.Generator.ApplyTerrainBrush(mod.ExistingData, mod.Brush, job.Context, mod.IsAdding, job.Token);
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
