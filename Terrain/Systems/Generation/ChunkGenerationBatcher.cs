using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkGenerationBatcher
{
    private List<ChunkGenerationJob> queue = new List<ChunkGenerationJob>();
    private readonly object queueLock = new();

    public int Count => queue.Count;
    public bool HasPending => queue.Count > 0;

    public void Add(ChunkGenerationJob job)
    {
        lock (queueLock)
        {
            this.queue.Add(job);
        }
    }

    public bool Remove(ChunkKey key)
    {
        var exists = queue.FirstOrDefault(r => r.Key.Equals(key));
        if (exists != null)
        {
            queue.Remove(exists);
            return true;
        }

        return false;
    }

    public List<ChunkGenerationJob> TryBatch(int batchSize)
    {
        if (queue.Count == 0)
            return null;

        int count = Math.Min(batchSize, queue.Count);
        List<ChunkGenerationJob> batch = queue.GetRange(0, count);
        queue.RemoveRange(0, count);

        return batch;
    }

}