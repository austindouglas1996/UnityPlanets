namespace GingerVoxelSystem.Systems.Generation
{
    using System;
    using System.Collections.Generic;
    using GingerVoxelSystem.Core;

    /// <summary>
    /// Queues <see cref="ChunkGenerationJob"/> instances for later processing, 
    /// allowing them to be batched in smaller groups to avoid performance spikes.
    /// </summary>
    public class ChunkGenerationBatcher
    {
        /// <summary>
        /// Makes for an easier way to check for a key in the system.
        /// </summary>
        private readonly Queue<ChunkKey> keys = new();

        /// <summary>
        /// A collection of items in this batch.
        /// </summary>
        private readonly Dictionary<ChunkKey, ChunkGenerationJob> jobs = new();

        /// <summary>
        /// Returns the amount of items in this batcher.
        /// </summary>
        public int Count => jobs.Count;

        /// <summary>
        /// Returns whether there is pending entries within this batch.
        /// </summary>
        public bool HasPending => jobs.Count > 0;

        /// <summary>
        /// Adds a job to the batcher.  
        /// Jobs with duplicate keys are ignored.
        /// </summary>
        /// <returns><c>true</c> if the job was added; <c>false</c> if it already exists.</returns>
        public bool Add(ChunkGenerationJob job)
        {
            if (jobs.ContainsKey(job.Key))
                return false;

            jobs.Add(job.Key, job);
            keys.Enqueue(job.Key);

            return true;
        }

        /// <summary>
        /// Removes a job by key, if present.  
        /// Note: this does not remove the key from the internal queue; it will be skipped during batching.
        /// </summary>
        public bool Remove(ChunkKey key)
        {
            return jobs.Remove(key);
        }

        /// <summary>
        /// Remove all items.
        /// </summary>
        public void Clear()
        {
            this.jobs.Clear();  
        }

        /// <summary>
        /// Dequeues up to <paramref name="maxCount"/> jobs into <paramref name="dest"/>.  
        /// Keys with no corresponding job (removed earlier) are skipped.  
        /// Individual lookups are O(1), but total cost is O(N) for the number of jobs pulled.
        /// </summary>
        /// <returns>The number of jobs added to <paramref name="dest"/>.</returns>
        public int TryBatch(int maxCount, List<ChunkGenerationJob> dest)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            dest.Clear();
            if (Count == 0 || jobs.Count == 0) return 0;

            while (dest.Count < maxCount && keys.Count > 0)
            {
                var k = keys.Dequeue();
                if (!jobs.TryGetValue(k, out var job)) continue;

                jobs.Remove(k);
                dest.Add(job);
            }

            return dest.Count;
        }
    }
}