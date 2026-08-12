namespace Assets.Scripts.Terrain.Systems.Rendering.Unity
{
    using GingerVoxelSystem.Systems.Rendering;
    using System;
    using System.Collections.Generic;
    using UnityEngine.Rendering;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Systems.Generation;

    /// <summary>
    /// Thin “lane switch” over two <see cref="ChunkRenderBucketCollection"/>s:
    /// one for LOD0, one for everything else. I hand it <see cref="ChunkKey"/>s and
    /// it routes them to the right pool; I call <see cref="Update"/> / <see cref="Draw"/> once.
    /// No Unity-specific bits here except the draw material.
    /// </summary>
    /// <remarks>
    /// - Not thread-safe; call from the main Unity thread.
    /// - LOD0 physics/collider hookups lives in the MonoBehaviour; this just routes keys.
    /// </remarks>
    public class ChunkRenderRouter : IDisposable
    {
        /// <summary>
        /// A collection of main buckets used throughout generation.
        /// </summary>
        private List<ChunkRenderBucketCollection> lodBuckets = new();
        private readonly Dictionary<ChunkRenderBucket, List<ChunkGenerationJob>> callBacks = new();

        private IChunkGenerator chunkGenerator;

        /// <summary>
        /// Build two lanes with their own capacities/thresholds.
        /// </summary>
        /// <param name="chunkGenerator">Shared generator used by both lanes.</param>
        /// <param name="mainCap">Max items per non-LOD0 bucket (default: 128).</param>
        /// <param name="mainThres">Removals before a non-LOD0 bucket regenerates.</param>
        /// <param name="lod0Cap">Max items per LOD0 bucket (keep this small: 24–32 is nice).</param>
        /// <param name="lod0Thres">Removals before an LOD0 bucket regenerates.</param>
        public ChunkRenderRouter(IChunkServices services, IChunkGenerator chunkGenerator, int capPerBucket)
        {
            this.chunkGenerator = chunkGenerator;

            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, capPerBucket));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, capPerBucket));

            lodBuckets[0].BucketRemoved += Bucket_Removed;
            lodBuckets[1].BucketRemoved += Bucket_Removed;
        }

        /// <summary>
        /// Route a key into the right lane by its <see cref="ChunkKey.LODIndex"/>.
        /// Collection handles dedupe; this is a simple forwarder.
        /// </summary>
        public void Add(ChunkGenerationJob job)
        {
            var bucket = lodBuckets[job.Key.LODIndex == 0 ? 0 : 1].Add(job.Key);

            // Ensure list exists
            if (!callBacks.TryGetValue(bucket, out var list))
            {
                callBacks[bucket] = new();

                // Subscribe exactly once
                bucket.OnGenerate += Bucket_OnGenerate;
            }

            callBacks[bucket].Add(job);
        }

        /// <summary>
        /// Remove a key from whichever lane owns it.
        /// Returns true if the key existed and was removed.
        /// </summary>
        public bool Remove(ChunkKey key)
        {
            return lodBuckets[key.LODIndex == 0 ? 0 : 1].Remove(key);
        }

        public bool Exists(ChunkKey key)
        {
            return lodBuckets[key.LODIndex == 0 ? 0 : 1].Exists(key);
        }

        /// <summary>
        /// Edit a given entity so its bucket collection is regenerated.
        /// </summary>
        /// <param name="key"></param>
        public void Edit(ChunkKey key)
        {
            lodBuckets[key.LODIndex == 0 ? 0 : 1].Edit(key);
        }

        /// <summary>
        /// Remove all elements.
        /// </summary>
        public void Clear()
        {
            foreach (var bucket in lodBuckets)
            {
                bucket.Clear();
            }
        }

        /// <summary>
        /// Tick both lanes. Buckets handle their own debounce/regeneration internally.
        /// </summary>
        public void Update()
        {
            foreach (var bucket in lodBuckets)
            {
                bucket.Update();
            }
        }

        /// <summary>
        /// Dispose both lanes and release GPU memory.
        /// Safe to call during teardown.
        /// </summary>
        public void Dispose()
        {
            foreach (var bucket in lodBuckets)
                bucket.Dispose();

            lodBuckets = null;
        }

        /// <summary>
        /// Fille the <see cref="CommandBuffer"/> with our buckets chunks.
        /// </summary>
        /// <param name="cmd"></param>
        public void FillCommandBuffer(RasterCommandBuffer cmd)
        {
            if (lodBuckets == null) return;

            foreach (var bucket in lodBuckets)
                bucket.Draw(cmd, chunkGenerator.GetMaterial);
        }

        /// <summary>
        /// Tell both lanes to regenerate. 
        /// <paramref name="force"/> skips their small debounce (use sparingly).
        /// </summary>
        public void MarkAsDirty(bool force)
        {
            foreach (var bucket in lodBuckets)
            {
                bucket.MarkAsDirty(force);
            }
        }

        /// <summary>
        /// Call collected <see cref="ChunkGenerationJob"/>s and set them to complete.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bucket_OnGenerate(object sender, EventArgs e)
        {
            var bucket = (ChunkRenderBucket)sender;

            // If bucket was removed mid-frame, ignore safely
            if (!callBacks.TryGetValue(bucket, out var list))
                return;

            // Notify all jobs waiting on this bucket
            foreach (var job in list)
            {
                if (job.OnDone != null)
                    job.OnDone(job.Key, job.ParentIndex, true);
            }

            list.Clear();
        }

        /// <summary>
        /// Call when a bucket is removed we no longer need to watch for jobs.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bucket_Removed(object sender, EventArgs e)
        {
            var bucket = (ChunkRenderBucket)sender;

            // If we tracked callbacks for this bucket, remove them & unsubscribe
            if (callBacks.Remove(bucket))
            {
                bucket.OnGenerate -= Bucket_OnGenerate;
            }
        }
    }
}