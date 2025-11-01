using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A collection of chunk keys to help with distributing render data.
/// </summary>
public class ChunkRenderBucket : IDisposable
{
    /// <summary>
    /// The collection of items used in this bucket.
    /// </summary>
    private ChunkKey?[] items;

    /// <summary>
    /// Side index so Contains/Remove are O(1). No linear scans.
    /// </summary>
    private readonly Dictionary<ChunkKey, int> index;

    /// <summary>
    /// A list of available spots.
    /// </summary>
    private Queue<int> AvailableSlots;

    /// <summary>
    /// A list of modified positions since the last generation.
    /// </summary>
    private readonly Dictionary<int, ChunkKey?> modifications;

    /// <summary>
    /// The last index used.
    /// </summary>
    private int lastIndex;

    /// <summary>
    /// The allowed amount of elements in this bucket.
    /// </summary>
    private int capacity = 0;

    /// <summary>
    /// Tells whether this bucket should request itself to be re-generated.
    /// </summary>
    private bool IsDirty = false;

    /// <summary>
    /// Helper so we don't accidently requeue a generation request if the job is taking longer
    /// than expected. This can happen in larger generations.
    /// </summary>
    private bool GenerateInProgress = false;

    /// <summary>
    /// Give a small delay on tickets to update this way we get as many updates as possible.
    /// </summary>
    private int RemainingTicksToUpdate = 5;

    /// <summary>
    /// The generator used to dispatch generation requests.
    /// </summary>
    private IChunkGenerator chunkGenerator;

    /// <summary>
    /// The material block used for generation.
    /// </summary>
    private MaterialPropertyBlock mpb = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ChunkRenderBucket"/>.
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="rebuildThreshhold"></param>
    /// <param name="chunkGenerator"></param>
    public ChunkRenderBucket(int capacity, IChunkGenerator chunkGenerator)
    {
        this.items = new ChunkKey?[capacity];
        this.index = new(capacity);

        this.capacity = capacity;
        this.AvailableSlots = new Queue<int>(capacity);
        this.modifications = new(capacity);

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
    public virtual bool IsFull => index.Count >= capacity;

    /// <summary>
    /// Has this bucket no items?
    /// </summary>
    public bool IsEmpty => index.Count == 0;

    /// <summary>
    /// Fast append. If we add anything, mark dirty so we rebuild soon.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>True if added successfully.</returns>
    public bool TryAdd(ChunkKey key)
    {
        if (index.ContainsKey(key) || IsFull) return false;

        int pos;
        if (this.AvailableSlots.Count != 0)
        {
            pos = this.AvailableSlots.Dequeue();
            items[pos] = key;
            modifications[pos] = key;
        }
        else
        {
            pos = lastIndex;
            items[lastIndex] = key;
            lastIndex++;
        }

        index[key] = pos;

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
        if (!index.TryGetValue(key, out int i))
            return false;

        if (i == lastIndex -1)
        {
            lastIndex--;
        }

        items[i] = null;
        index.Remove(key);
        modifications[i] = null;
        AvailableSlots.Enqueue(i);
        this.MarkAsDirty(false);

        return true;
    }

    /// <summary>
    /// Remove all elements from this collection.
    /// </summary>
    public void Clear()
    {
        this.items = null;
        this.index.Clear();
        this.AvailableSlots.Clear();
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
        // In some cases as we are getting new data we want to delay the update
        // this way we dont regenerate this bucket just for it go through another
        // immediate update.
        if (RemainingTicksToUpdate > 0)
        {
            RemainingTicksToUpdate--;
            return;
        }

        if (IsDirty && !this.GenerateInProgress)
        {
            this.Generate();
        }
    }

    /// <summary>
    /// Have the bucket draw the render data which includes the elements from this bucket.
    /// </summary>
    /// <param name="vertexMat"></param>
    public void Draw(CommandBuffer cdb, Material vertexMat)
    {
        if (IsEmpty) return;

        var rd = RenderData;
        if (rd == null || rd.IsDisposed || rd.Triangle == null || rd.Args == null) return;

        // enqueue indirect procedural draw
        cdb.DrawProceduralIndirect(
            Matrix4x4.identity,
            vertexMat,
            0,          
            MeshTopology.Triangles,
            rd.Args,
            0, mpb);
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
    protected virtual void GenerateCore(ChunkKey?[] items, Dictionary<int, ChunkKey?> modifications, Action<ChunkRenderBatch> onDone)
    {
        chunkGenerator.DispatchGeneration(items, modifications, onDone, this.renderData);
    }

    /// <summary>
    /// A pregeneration sort function to sort the collection before sending to dispatch.
    /// </summary>
    protected virtual void PreGenerateSort()
    {
        int last = lastIndex - 1;

        while (AvailableSlots.Count > 0)
        {
            int i = AvailableSlots.Dequeue();
            if (i >= last)
            {
                continue;
            }

            ChunkKey lastItem = items[last].Value;

            // Modify the modification.
            modifications[i] = lastItem;

            // Move last item into hole.
            items[i] = lastItem;
            index[lastItem] = i;

            // Clear tail
            items[last] = null;
            last--;
            lastIndex--;
        }
    }

    /// <summary>
    /// Call ths bucket with the included elements to be generated.
    /// </summary>
    private void Generate()
    {
        if (GenerateInProgress || this.IsEmpty) return;
        GenerateInProgress = true;

        PreGenerateSort();
        GenerateCore(items, modifications, OnGenerateCompleted);

        this.modifications.Clear();
    }

    /// <summary>
    /// Handle execution for <see cref="Generate"/>. 
    /// </summary>
    /// <remarks>This was changed from a lambda as I track down a stuttering issue and GC issues.</remarks>
    /// <param name="output"></param>
    private void OnGenerateCompleted(ChunkRenderBatch output)
    {
        RenderData = output;

        // Something went wrong to reach this.
        if (this.RenderData == null)
        {
            this.IsDirty = false;
            this.GenerateInProgress = false;
            return;
        }

        OnGenerate?.Invoke(this, EventArgs.Empty);

        mpb.SetBuffer("_TriangleBuffer", RenderData.Triangle);
        mpb.SetBuffer("_TriangleDetailsBuffer", RenderData.Details);

        this.IsDirty = false;
        this.GenerateInProgress = false;
    }
}