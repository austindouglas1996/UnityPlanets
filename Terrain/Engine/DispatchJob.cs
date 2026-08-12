namespace GingerVoxelSystem.Engine
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Systems.Rendering;
    using System;
    using System.Collections.Generic;

    public struct DispatchJob
    {
        public readonly ChunkKey?[] Keys;
        public readonly int KeysCount;
        public readonly Dictionary<int, ChunkKey?> Modifications;

        // The reusable GPU workspace for this chunk-group
        public ChunkRenderBatch Batch;

        // Callback after generation completes
        public readonly Action<ChunkRenderBatch> OnCompleted;

        public DispatchJob(
            ChunkKey?[] keys,
            int keysCount,
            Dictionary<int, ChunkKey?> modifications,
            ChunkRenderBatch batch,
            Action<ChunkRenderBatch> onCompleted)
        {
            Keys = keys;
            KeysCount = keysCount;
            // Aliased, not copied, to avoid a Dictionary allocation per dispatch.
            // Safe because the only consumer (ChunkBuffers.GroupContiguous) snapshots
            // the keys synchronously at the start of DispatchGeneration, before the
            // owning bucket clears this dictionary.
            // If generation ever becomes async/deferred, restore the defensive copy.
            Modifications = modifications;
            Batch = batch;
            OnCompleted = onCompleted;
        }
    }

}
