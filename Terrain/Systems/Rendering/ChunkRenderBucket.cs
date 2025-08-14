using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A collection of chunk keys to help with distributing render data.
/// </summary>
public class ChunkRenderBucket : IDisposable
{
    private readonly List<ChunkKey> items;

    /// <summary>
    /// Side index so Contains/Remove are O(1). No linear scans.
    /// </summary>
    private readonly Dictionary<ChunkKey, int> index;

    private int capacity = 0;
    private int rebuildThreshold = 0;
    private int removedCount = 0;

    private bool IsDirty = false;
    private int RemainingTicksToUpdate = 5;

    private IChunkGenerator chunkGenerator;
    private GPUSet RenderData;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="rebuildThreshhold"></param>
    /// <param name="chunkGenerator"></param>
    public ChunkRenderBucket(int capacity, int rebuildThreshhold, IChunkGenerator chunkGenerator)
    {
        this.items = new(capacity);
        this.index = new(capacity);

        this.capacity = capacity;
        this.rebuildThreshold = rebuildThreshhold;

        this.chunkGenerator = chunkGenerator;
    }

    /// <summary>
    /// Has this bucket reached maximum capacity.
    /// </summary>
    public bool IsFull => items.Count == capacity;

    /// <summary>
    /// Fast append. If we add anything, mark dirty so we rebuild soon.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>True if added successfully.</returns>
    public bool TryAdd(ChunkKey key)
    {
        if (index.ContainsKey(key) || items.Count >= capacity) return false;

        index[key] = items.Count;
        items.Add(key);

        this.MarkAsDirty();

        return true;
    }

    /// <summary>
    /// O(1) remove via swap-back. Order does NOT matter here.
    /// If we remove "enough", flip dirty so we rebuild next chance we get.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool TryRemove(ChunkKey key)
    {
        if (!index.TryGetValue(key, out int i)) return false;

        int last = items.Count - 1;
        var swap = items[last];

        items[i] = swap;
        items.RemoveAt(last);

        index[swap] = i;
        index.Remove(key);

        removedCount++;
        if (removedCount >= rebuildThreshold)
        {
            this.MarkAsDirty();
        }

        return true;
    }

    /// <summary>
    /// Returns whether the element exists within this bucket.
    /// </summary>
    /// <param name="k"></param>
    /// <returns></returns>
    public bool Contains(ChunkKey k) => index.ContainsKey(k);

    /// <summary>
    /// Update the bucket by scheduling generation, or removal of elements.
    /// </summary>
    public void Update()
    {
        if (RemainingTicksToUpdate > 0)
        {
            RemainingTicksToUpdate--;
            return;
        }

        if (IsDirty)
        {
            this.Generate();
        }
    }

    /// <summary>
    /// Have the bucket draw the render data which includes the elements from this bucket.
    /// </summary>
    /// <param name="vertexMat"></param>
    public void Draw(Material vertexMat)
    {
        vertexMat.SetBuffer("_TriangleBuffer", RenderData.Triangle);
        //vertexMat.SetPass(0);
        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, RenderData.Args, 0);
    }

    /// <summary>
    /// Mark this bucket as dirty to request a regeneration. Optionally force the update to happen now.
    /// </summary>
    /// <param name="forceNow">The update will happen right away and not consider its options.</param>
    public void MarkAsDirty(bool forceNow)
    {
        this.IsDirty = true;

        if (forceNow || this.IsFull)
            this.RemainingTicksToUpdate = 0;
        else
            this.RemainingTicksToUpdate = 5;
    }

    /// <summary>
    /// Dispose of the renderData and release memory.
    /// </summary>
    public void Dispose()
    {
        this.RenderData.Dispose();
    }

    /// <summary>
    /// Call ths bucket with the included elements to be generated.
    /// </summary>
    private void Generate()
    {
        RenderData?.Dispose();
        RenderData = chunkGenerator.DispatchGeneration(this.items);
        this.IsDirty = false;
    }
}