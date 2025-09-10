using System;
using System.Collections.Generic;
using UnityEngine;

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
    private bool GenerateInProgress = false;

    /// <summary>
    /// Give a small delay on tickets to update this way we get as many updates as possible.
    /// </summary>
    private int RemainingTicksToUpdate = 5;

    private IChunkGenerator chunkGenerator;

    /// <summary>
    /// Initializes a new instance of <see cref="ChunkRenderBucket"/>.
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="rebuildThreshhold"></param>
    /// <param name="chunkGenerator"></param>
    public ChunkRenderBucket(int capacity, int rebuildThreshhold, IChunkGenerator chunkGenerator)
    {
        this.items = new(capacity);
        this.index = new(capacity);

        this.capacity = capacity;
        this.rebuildThreshold = 1;

        this.chunkGenerator = chunkGenerator;
    }

    /// <summary>
    /// Gets the render batch data created during <see cref="Generate"/>
    /// </summary>
    public ChunkRenderBatch RenderData
    {
        get { return renderData; }
        private set { renderData = value; }
    }
    private ChunkRenderBatch renderData;

    /// <summary>
    /// Method called on <see cref="Generate"/> call.
    /// </summary>
    public event EventHandler OnGenerate;

    /// <summary>
    /// Has this bucket reached maximum capacity.
    /// </summary>
    public virtual bool IsFull => items.Count == capacity;

    /// <summary>
    /// Has this bucket no items?
    /// </summary>
    public bool IsEmpty => items.Count == 0;

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

        this.MarkAsDirty(false);

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
            this.MarkAsDirty(false);
        }

        return true;
    }

    /// <summary>
    /// Remove all elements from this collection.
    /// </summary>
    public void Clear()
    {
        this.items.Clear();
        this.index.Clear();
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

        if (IsDirty & !this.GenerateInProgress)
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
        if (items.Count == 0) return;

        var rd = RenderData;
        if (rd == null || rd.Triangle == null || rd.Args == null) return;

        vertexMat.SetBuffer("_TriangleBuffer", RenderData.Triangle);
        vertexMat.SetPass(0);

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
        if (this.RenderData != null)
        {
            this.RenderData.Dispose();
            this.RenderData = null;
        }
    }

    /// <summary>
    /// Core logic that actually performs generation. Override in subclasses.
    /// </summary>
    protected virtual void GenerateCore(List<ChunkKey> items, Action<ChunkRenderBatch> onDone)
    {
        chunkGenerator.DispatchGeneration(items, onDone);
    }

    /// <summary>
    /// Call ths bucket with the included elements to be generated.
    /// </summary>
    private void Generate()
    {
        if (GenerateInProgress)
            return;
        GenerateInProgress = true;

        if (this.items.Count != 0)
        {
            GenerateCore(items, (ChunkRenderBatch output) =>
            {
                RenderData?.Dispose();
                RenderData = null;

                RenderData = output;

                this.IsDirty = false;
                this.GenerateInProgress = false;

                // Reset this back to zero.
                removedCount = 0;

                // Something went wrong to reach this.
                if (this.RenderData == null)
                    return;

                OnGenerate?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}