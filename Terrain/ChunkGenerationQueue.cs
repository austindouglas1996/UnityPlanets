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
    /// The follower used to calculate priority.
    /// </summary>
    private Transform follower;

    /// <summary>
    /// The generator used to generate.
    /// </summary>
    private IChunkGenerator chunkGenerator;

    /// <summary>
    /// The configuration used for chunk generation.
    /// </summary>
    private IChunkConfiguration chunkConfiguration;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkGenerationQueue"/> class.
    /// </summary>
    public ChunkGenerationQueue(Transform follower, IChunkGenerator chunkGenerator, IChunkConfiguration configuration, CancellationToken token)
    {
        if (follower == null)
            throw new ArgumentNullException(nameof(follower));
        if (chunkGenerator == null)
            throw new ArgumentNullException(nameof(chunkGenerator));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        this.follower = follower;
        this.chunkGenerator = chunkGenerator;
        this.chunkConfiguration = configuration;
        this.cancellationToken = token;

        for (int i = 0; i < 3; i++)
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

            // Create new key.
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            ChunkGenerationJob newJob = new(coordinates, LODIndex, cts);

            // Register job as active
            pendingJobs[coordinates] = newJob;
            generationQueue.Enqueue(newJob, GetPriorityOfChunk(coordinates));

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
                job.Completion.TrySetCanceled();
                continue;
            }

            try
            {
                var result = await chunkGenerator.GenerateNewChunk(job.Coordinates, job.LODIndex, chunkConfiguration, job.Token);
                job.Completion.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                job.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                job.Completion.TrySetException(ex);
            }

            await Task.Yield(); // Yield to prevent thread starvation
        }
    }

    /// <summary>
    /// Return the priority of this chunk based on the distance from the follower.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    private int GetPriorityOfChunk(Vector3Int coordinates)
    {
        Vector3 worldPos = this.follower.transform.position;

        Vector2Int followerChunkCoord = new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkConfiguration.ChunkSize),
            Mathf.FloorToInt(worldPos.z / chunkConfiguration.ChunkSize));

        int dx = Mathf.Abs(coordinates.x - followerChunkCoord.x);
        int dz = Mathf.Abs(coordinates.z - followerChunkCoord.y);

        return Math.Max(dx, dz);
    }
}
