using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine;
using System.Threading;
using System.Collections.ObjectModel;

public class ChunkGenerationQueue
{
    /// <summary>
    /// A dictionary of active chunks with their jobs. Allows for one respective job, but also
    /// works with LOD so past jobs are cancelled before being queued. 
    /// </summary>
    private readonly Dictionary<Vector3Int, ChunkJob> pendingJobs = new Dictionary<Vector3Int, ChunkJob>();
    private readonly object pendingJobsLock = new();

    /// <summary>
    /// A collection of jobs to be executed yet. Seperate from active jobs, this runs the actual
    /// task.
    /// </summary>
    private Queue<ChunkJob> generationQueue = new Queue<ChunkJob>();

    /// <summary>
    /// A task to run the process queue.
    /// </summary>
    private Task? processQueueTask = null;
    private readonly object processLock = new();

    /// <summary>
    /// Options when running the job system.
    /// </summary>
    private int maxConcurrentTasks = 64;
    private int runningTasks = 0;
    private bool isProcessing = false;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkGenerationQueue"/> class.
    /// </summary>
    public ChunkGenerationQueue()
    {
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
    /// Returns an instance of <see cref="ChunkGenerationQueue"/> to keep the codebase a bit clean and make sure
    /// there is only one generation system so the CPU is not overloaded.
    /// </summary>
    public static ChunkGenerationQueue Instance
    {
        get
        {
            if (instance == null)
                instance = new ChunkGenerationQueue();

            return instance;
        }
    }
    private static ChunkGenerationQueue instance;

    /// <summary>
    /// Request chunk generation for a given chunk. Given LOD, certain details are required to generate an appropiate job.
    /// The job will then be tracked and cancelled if another job from the same coordinates is given. 
    /// </summary>
    /// <param name="coordinates"></param>
    /// <param name="LODIndex"></param>
    /// <param name="generationTask"></param>
    /// <returns></returns>
    public Task RequestChunkGeneration(Vector3Int coordinates, int LODIndex, Func<CancellationToken, Task> generationTask)
    {
        if (pendingJobs.TryGetValue(coordinates, out var job))
        {
            if (job.LODIndex == LODIndex)
                return job.Task;

            job.Cancel(); 
        }

        // Create new key.
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var wrappedTask = Task.Run(async () =>
        {
            try
            {
                await generationTask(token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Chunk cancelled.");
            }
            finally
            {
                lock (pendingJobsLock)
                {
                    if (pendingJobs.TryGetValue(coordinates, out var job) && job.LODIndex == LODIndex)
                    {
                        pendingJobs.Remove(coordinates);
                    }
                }
            }
        }, token);

        lock (pendingJobsLock)
        {
            ChunkJob newJob = new(coordinates, LODIndex, wrappedTask, cts);

            // Register job as active
            pendingJobs[coordinates] = newJob;

            // Only enqueue if not cancelled already
            if (!newJob.Token.IsCancellationRequested)
            {
                generationQueue.Enqueue(newJob);
            }
        }

        lock (processLock)
        {
            if (processQueueTask == null || processQueueTask.IsCompleted)
            {
                processQueueTask = ProcessQueue();
            }
        }

        return wrappedTask;
    }

    /// <summary>
    /// Process the job queue.
    /// </summary>
    /// <returns></returns>
    private async Task ProcessQueue()
    {
        isProcessing = true;
        int tasksPerFrame = 2; 

        while (generationQueue.Count > 0)
        {
            // This is only called if game closed.
            cancellationToken.ThrowIfCancellationRequested();

            int processedThisFrame = 0;
            while (generationQueue.Count > 0 && processedThisFrame < tasksPerFrame)
            {
                if (runningTasks > maxConcurrentTasks)
                    break;
                else
                    runningTasks++;

                ChunkJob job = generationQueue.Dequeue();

                if (job.Token.IsCancellationRequested)
                {
                    runningTasks--;
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await job.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log($"[ChunkGen] Cancelled chunk {job.Coordinates} (LOD {job.LODIndex})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Chunk generation error: {ex}");
                    }
                    finally
                    {
                        runningTasks--;
                        Debug.Log("Remaining tasks: " + generationQueue.Count + ", running: " + runningTasks);
                    }
                });

                processedThisFrame++;
            }

            await Task.Yield(); // yield after batch
        }

        isProcessing = false;
    }
}
