namespace GingerVoxelSystem.Systems.Generation
{
    using System;
    using GingerVoxelSystem.Core;

    /// <summary>
    /// Represents a single chunk generation request, containing its unique key 
    /// and a callback to invoke when processing is complete.
    /// </summary>
    public class ChunkGenerationJob
    {
        /// <summary>
        /// Creates a new chunk generation job.
        /// </summary>
        /// <param name="key">The unique identifier for the chunk to generate.</param>
        /// <param name="action">
        /// A callback invoked when the job completes. 
        /// The boolean parameter indicates success (<c>true</c>) or failure (<c>false</c>).
        /// </param>
        public ChunkGenerationJob(ChunkKey key, Action<ChunkKey, int, bool> action, int parentIndex = -1)
        {
            this.Key = key;
            this.ParentIndex = parentIndex;
            this.OnDone = action;
        }

        /// <summary>
        /// The unique identifier for the chunk.
        /// </summary>
        public ChunkKey Key;

        /// <summary>
        /// Used in some contexts.
        /// </summary>
        public int ParentIndex;

        /// <summary>
        /// The completion callback for this job.
        /// </summary>
        public Action<ChunkKey, int, bool> OnDone;
    }
}