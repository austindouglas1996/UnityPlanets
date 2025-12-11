namespace GingerVoxelSystem.Systems.Rendering
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using GingerVoxelSystem.Core;

    /// <summary>
    /// A collection container for <see cref="ChunkRenderBucket"/> that can grow as needed
    /// while keeping itself tidy. Think of it like an expandable <see cref="List{T}"/> of buckets
    /// where I don't care which bucket a key lands in—just that it lands somewhere fast.
    /// </summary>
    public class ChunkRenderBucketCollection : IDisposable
    {
        /// <summary>
        /// Buckets assigned with this collection.
        /// </summary>
        private List<ChunkRenderBucket> buckets = new();

        /// <summary>
        /// Buckets that were full but now have room again. I try to refill these first to keep buckets packed.
        /// </summary>
        private List<ChunkRenderBucket> bucketsWithSpace = new();

        /// <summary>
        /// Who owns a key right now? (instant remove, no scans)
        /// </summary>
        private readonly Dictionary<ChunkKey, ChunkRenderBucket> keys = new();

        /// <summary>
        /// The max amount of entries per bucket. Keeping this amount low, but not too low is important
        /// as every time an item is removed/added to a bucket it is resent back for generation.
        /// </summary>
        private int capacity = 128;

        /// <summary>
        /// An instance of the generator used for generation jobs.
        /// </summary>
        private IChunkGenerator chunkGenerator;

        /// <summary>
        /// Initalize a new instance of <see cref="ChunkRenderBucketCollection"/> with available capacity and rebuild thresholds.
        /// </summary>
        /// <param name="chunkGenerator">Shared generator used by all buckets.</param>
        /// <param name="capacity">Max items per bucket (default 128).</param>
        /// <param name="rebuiltThreshold">Removals per bucket before it regenerates.</param>
        public ChunkRenderBucketCollection(IChunkGenerator chunkGenerator, int capacity = 128)
        {
            this.chunkGenerator = chunkGenerator;
            this.capacity = capacity;
        }

        /// <summary>
        /// An event called when a bucket is removed;
        /// </summary>
        public event EventHandler BucketRemoved;

        /// <summary>
        /// Returns the amount of entries within this collection.
        /// </summary>
        public int KeysCount => keys.Count;

        /// <summary>
        /// Add a key to the collection. No-op if it already exists.
        /// Prefers refilling a previously-full bucket before touching the tail or creating a new one.
        /// </summary>
        /// <param name="key"></param>
        public ChunkRenderBucket Add(ChunkKey key)
        {
            if (keys.ContainsKey(key))
            {
                throw new System.ArgumentException("ChunkRenderBucketCollection tried to add an already existing key.");
            }

            var bucket = GetOrCreateTailBucket();

            if (bucket.TryAdd(key))
            {
                keys.Add(key, bucket);

                if (bucket.IsFull && bucketsWithSpace.Contains(bucket))
                    bucketsWithSpace.Remove(bucket);
            }
            else
            {
                throw new System.ArgumentException("Failed to add key into a bucket.");
            }

            return bucket;
        }

        /// <summary>
        /// Try to remove an existing key from our bucket collection.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(ChunkKey key)
        {
            if (!keys.ContainsKey(key)) return false;

            var bucket = keys[key];

            if (bucket.TryRemove(key))
            {
                this.keys.Remove(key);

                if (bucket.IsEmpty)
                {
                    if (bucketsWithSpace.Contains(bucket))
                        bucketsWithSpace.Remove(bucket);

                    this.buckets.Remove(bucket);

                    this.BucketRemoved?.Invoke(bucket, EventArgs.Empty);
                }
                else if (!bucketsWithSpace.Contains(bucket))
                    bucketsWithSpace.Add(bucket);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove all elements.
        /// </summary>
        public void Clear()
        {
            foreach (var bucket in buckets)
            {
                bucket.Clear();
            }

            keys.Clear();
        }

        /// <summary>
        /// Update the bucket collections.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < buckets.Count; i++)
                buckets[i].Update();
        }

        /// <summary>
        /// Draw the buckets render data.
        /// </summary>
        /// <param name="material"></param>
        public void Draw(CommandBuffer cdb, Material material)
        {
            for (int i = 0; i < buckets.Count; i++)
                buckets[i].Draw(cdb, material);
        }

        /// <summary>
        /// Dispose of each bucket.
        /// </summary>
        public void Dispose()
        {
            if (buckets != null)
            {
                for (int i = 0; i < buckets.Count; i++)
                    buckets[i].Dispose();
            }

            buckets = null;
        }

        /// <summary>
        /// Mark each bucket as dirty so they re-generate.
        /// </summary>
        /// <param name="force"></param>
        public void MarkAsDirty(bool force)
        {
            for (int i = 0; i < buckets.Count; i++)
                buckets[i].MarkAsDirty(force);
        }

        /// <summary>
        /// Pick where the next add goes:
        /// 1) a bucket that freed up (bucketsWithSpace[0]),
        /// 2) the current tail if it isn't full,
        /// 3) or create a new bucket.
        /// </summary>
        /// <returns></returns>
        private ChunkRenderBucket GetOrCreateTailBucket()
        {
            for (int i = 0; i < bucketsWithSpace.Count; i++)
            {
                var bucket = bucketsWithSpace[i];
                if (!bucket.GenerateInProgress)
                    return bucket;
            }

            if (buckets.Count == 0)
                return CreateBucket();

            var tail = buckets[^1];
            if (!tail.IsFull && !tail.GenerateInProgress) return tail;

            return CreateBucket();
        }

        /// <summary>
        /// Create a new bucket.
        /// </summary>
        /// <returns></returns>
        private ChunkRenderBucket CreateBucket()
        {
            ChunkRenderBucket newBucket;
            newBucket = new ChunkRenderBucket(capacity, this.chunkGenerator);

            buckets.Add(newBucket);

            return newBucket;
        }
    }
}