namespace GingerVoxelSystem.Systems.Rendering
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.Rendering;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Helpers;
    using GingerVoxelSystem.Systems.Generation;

    /// <summary>
    /// Thin “lane switch” over two <see cref="ChunkRenderBucketCollection"/>s:
    /// one for LOD0, one for everything else. I hand it <see cref="ChunkKey"/>s and
    /// it routes them to the right pool; I call <see cref="Update"/> / <see cref="Draw(Material)"/> once.
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

        private IChunkServices chunkServices;
        private IChunkGenerator chunkGenerator;

        /// <summary>
        /// Build two lanes with their own capacities/thresholds.
        /// </summary>
        /// <param name="chunkGenerator">Shared generator used by both lanes.</param>
        /// <param name="mainCap">Max items per non-LOD0 bucket (default: 128).</param>
        /// <param name="mainThres">Removals before a non-LOD0 bucket regenerates.</param>
        /// <param name="lod0Cap">Max items per LOD0 bucket (keep this small: 24–32 is nice).</param>
        /// <param name="lod0Thres">Removals before an LOD0 bucket regenerates.</param>
        public ChunkRenderRouter(IChunkServices services, IChunkGenerator chunkGenerator, int mainCap, int lod0Cap)
        {
            this.chunkServices = services;
            this.chunkGenerator = chunkGenerator;

            // This is still in testing. Add/remove a new collection as LODs increase
            // currently only LOD0 is allowed collision as its CPU intensive.
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, true, lod0Cap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
            lodBuckets.Add(new ChunkRenderBucketCollection(chunkGenerator, false, mainCap));
        }

        /// <summary>
        /// Route a key into the right lane by its <see cref="ChunkKey.LODIndex"/>.
        /// Collection handles dedupe; this is a simple forwarder.
        /// </summary>
        public void Add(ChunkGenerationJob job)
        {
            lodBuckets[job.Key.LODIndex].Add(job.Key);
        }

        /// <summary>
        /// Remove a key from whichever lane owns it.
        /// Returns true if the key existed and was removed.
        /// </summary>
        public bool Remove(ChunkKey key)
        {
            return lodBuckets[key.LODIndex].Remove(key);
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
            ConsoleTimer.Start("ChunkRouter.Update");

            foreach (var bucket in lodBuckets)
            {
                bucket.Update();
            }

            ConsoleTimer.Stop("ChunkRouter.Update");
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
        public void FillCommandBuffer(CommandBuffer cmd)
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
    }
}