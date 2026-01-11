namespace GingerVoxelSystem.Systems.Rendering
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
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
        private int nextIndex;

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
        public bool GenerateInProgress = false;

        /// <summary>
        /// Give a small delay on tickets to update this way we get as many updates as possible.
        /// </summary>
        private int RemainingTicksToUpdate = 15;

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
            this.capacity = capacity;

            this.items = new ChunkKey?[capacity];
            this.index = new(capacity);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(ChunkKey key)
        {
            if (index.ContainsKey(key))
            {
                Debug.LogError("ChunkRenderBucket tried to add an existing key.");
                return false;
            }

            if (IsFull) return false;

            int pos;
            if (AvailableSlots.Count != 0)
            {
                int hole = AvailableSlots.Dequeue();

                // Stale hole if it’s at/after the frontier. append instead
                if (hole >= nextIndex)
                {
                    pos = nextIndex;
                    nextIndex++;
                }
                else
                {
                    pos = hole;
                }
            }
            else
            {
                pos = nextIndex;
                nextIndex++;
            }

            items[pos] = key;
            index[key] = pos;
            modifications[pos] = key;
            MarkAsDirty(false);
            return true;
        }

        /// <summary>
        /// O(1) remove via swap-back. Order does NOT matter here.
        /// If we remove "enough", flip dirty so we rebuild next chance we get.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(ChunkKey key)
        {
            return TryRemoveInternal(key);
        }

        /// <summary>
        /// O(1) remove via swap-back. Order does NOT matter here.
        /// If we remove "enough", flip dirty so we rebuild next chance we get.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ignoreQueue"></param>
        /// <returns></returns>
        private bool TryRemoveInternal(ChunkKey key, bool ignoreQueue = false)
        {
            if (!index.TryGetValue(key, out int i))
            {
                Debug.LogWarning("ChunkRenderBucket tried to remove an invalid key.");
                return false;
            }

            index.Remove(key);
            items[i] = null;

            // If ignoreQueu is true we are doing some house cleaning
            // and do not want 
            if (!ignoreQueue)
                modifications[i] = null;

            if (i == nextIndex - 1)
            {
                nextIndex--;
                while (nextIndex > 0 && items[nextIndex - 1] == null)
                    nextIndex--;
            }
            else
            {
                // If this function is being called internally in side this class.
                // we may want to avoid adding it to the queue as we are doing a cleanup.
                if (!ignoreQueue)
                    AvailableSlots.Enqueue(i);
            }

            this.MarkAsDirty(false);

            return true;
        }

        /// <summary>
        /// Modify an existing key with details that have been modified.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool TryModify(ChunkKey key)
        {
            if (!index.TryGetValue(key, out int pos))
            {
                Debug.LogWarning("ChunkRenderBucket tried to modify an invalid key.");
                return false;
            }

            items[pos] = key;
            index[key] = pos;
            modifications[pos] = key;
            MarkAsDirty(false);

            return true;
        }

        /// <summary>
        /// Remove all elements from this collection.
        /// </summary>
        public void Clear()
        {
            Array.Clear(items, 0, nextIndex);
            nextIndex = 0;

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
            if (!IsDirty) return;

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
                this.DispatchGeneration();
            }
        }

        /// <summary>
        /// Have the bucket draw the render data which includes the elements from this bucket.
        /// </summary>
        /// <param name="vertexMat"></param>
        public void Draw(RasterCommandBuffer cdb, Material vertexMat)
        {
            if (IsEmpty) return;

            var rd = RenderData;
            if (rd == null || rd.IsDisposed || rd.RawTriangleBuffer == null || rd.Args == null) return;

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
        /// Mark this bucket as dirty to request a regeneration. Optionally force the update to happen now.
        /// </summary>
        /// <param name="forceNow">The update will happen right away and not consider its options.</param>
        public void MarkAsDirty(bool forceNow)
        {
            this.IsDirty = true;

            if (forceNow)
                this.RemainingTicksToUpdate = 0;
            else
                this.RemainingTicksToUpdate = 25;
        }

        /// <summary>
        /// A pregeneration sort function to sort the collection before sending to dispatch.
        /// </summary>
        protected virtual void PreDispatchGeneration()
        {
            while (AvailableSlots.Count > 0 && nextIndex - 1 >= 0)
            {
                var item = items[nextIndex - 1].Value;
                TryRemoveInternal(item, true);
                TryAdd(item);
            }
        }

        /// <summary>
        /// Call the <see cref="IChunkGenerator"/> dispatch function to deploy this batch for generation.
        /// </summary>
        protected virtual void OnDispatchGeneration()
        {
            DispatchJob job = new DispatchJob(items, nextIndex, modifications, this.renderData, OnDispatchGenerationCompleted);
            chunkGenerator.DispatchGeneration(job);
        }

        /// <summary>
        /// Handle execution for <see cref="Generate"/>. 
        /// </summary>
        /// <remarks>This was changed from a lambda as I track down a stuttering issue and GC issues.</remarks>
        /// <param name="output"></param>
        protected virtual void OnDispatchGenerationCompleted(ChunkRenderBatch output)
        {
            if (RenderData != output)
            {
                RenderData = output;
                mpb.SetBuffer("_TriangleBuffer", RenderData.FlatTriangleBuffer);
                mpb.SetBuffer("_TriangleDetailsBuffer", RenderData.Details);
            }

            OnGenerate?.Invoke(this, EventArgs.Empty);

            this.IsDirty = false;
            this.GenerateInProgress = false;
        }

        /// <summary>
        /// Call ths bucket with the included elements to be generated.
        /// </summary>
        private void DispatchGeneration()
        {
            if (GenerateInProgress || this.IsEmpty) return;
            GenerateInProgress = true;

            PreDispatchGeneration();
            OnDispatchGeneration();

            this.modifications.Clear();
        }
    }
}