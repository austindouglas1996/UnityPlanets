using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine;

public class ChunkGenerationQueue
{
    private Queue<Func<Task>> generationQueue = new Queue<Func<Task>>();
    private int maxConcurrentTasks = 64;
    private int runningTasks = 0;
    private bool isProcessing = false;

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

    public Task Enqueue(Func<Task> generationTask)
    {
        var tcs = new TaskCompletionSource<bool>();

        generationQueue.Enqueue(async () =>
        {
            try
            {
                await generationTask();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!isProcessing)
            ProcessQueue();

        return tcs.Task;
    }

    public Task<T> Enqueue<T>(Func<Task<T>> generationTask)
    {
        var tcs = new TaskCompletionSource<T>();

        generationQueue.Enqueue(async () =>
        {
            try
            {
                T result = await generationTask();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!isProcessing)
            ProcessQueue();

        return tcs.Task;
    }

    private async void ProcessQueue()
    {
        isProcessing = true;

        int tasksPerFrame = 5; 

        while (generationQueue.Count > 0)
        {
            int processedThisFrame = 0;

            while (generationQueue.Count > 0 && processedThisFrame < tasksPerFrame)
            {
                if (runningTasks < maxConcurrentTasks)
                {
                    var taskFunc = generationQueue.Dequeue();
                    runningTasks++;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await taskFunc();
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
                else
                {
                    break;
                }
            }

            await Task.Yield(); // yield after batch
        }

        isProcessing = false;
    }

}
