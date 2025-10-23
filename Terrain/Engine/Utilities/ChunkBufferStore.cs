using System;
using System.Collections.Generic;
using UnityEngine;

public class ChunkBufferStore
{
    private readonly int stride;
    private readonly ComputeBufferType type;

    // Key = capacity (elements), Value = list of reusable buffers
    private readonly Dictionary<int, List<ComputeBuffer>> pools = new();

    // Define every pool size you want once here
    private static readonly int[] PoolSizes = { 5_000, 20_000, 30_000, 40_000, 50_000, 60_000 };

    public ChunkBufferStore(int stride, ComputeBufferType type)
    {
        this.stride = stride;
        this.type = type;

        foreach (int size in PoolSizes)
            pools[size] = new List<ComputeBuffer>();
    }

    public ComputeBuffer GetBuffer(int chunks)
    {
        int capacity = GetPoolCapacity(chunks);
        var list = pools[capacity];

        if (list.Count == 0)
            list.Add(new ComputeBuffer(capacity, stride, type));

        var buf = list[^1];
        list.RemoveAt(list.Count - 1);
        buf.SetCounterValue(0);
        return buf;
    }

    public ComputeBuffer CheckOrGetBuffer(ComputeBuffer existing, int chunks)
    {
        ReleaseBuffer(existing);
        return GetBuffer(chunks);
    }

    public void ReleaseBuffer(ComputeBuffer buffer)
    {
        if (buffer == null || !buffer.IsValid()) return;

        int nearest = GetNearestPool(buffer.count);
        pools[nearest].Add(buffer);
    }

    private int GetPoolCapacity(int chunks)
    {
        float ratio = chunks / 64f;

        if (ratio <= 0.10f) return 5_000;
        if (ratio <= 0.25f) return 20_000;
        if (ratio <= 0.50f) return 30_000;
        if (ratio <= 0.75f) return 40_000;
        if (ratio <= 0.90f) return 50_000;
        return 60_000;
    }

    private int GetNearestPool(int count)
    {
        int nearest = PoolSizes[0];
        int bestDiff = Math.Abs(PoolSizes[0] - count);

        for (int i = 1; i < PoolSizes.Length; i++)
        {
            int diff = Math.Abs(PoolSizes[i] - count);
            if (diff < bestDiff)
            {
                nearest = PoolSizes[i];
                bestDiff = diff;
            }
        }

        return nearest;
    }

    public void ReleaseAll()
    {
        foreach (var kvp in pools)
        {
            foreach (var buf in kvp.Value)
                buf.Release();
            kvp.Value.Clear();
        }
    }
}
