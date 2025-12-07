using GingerVoxelSystem.Core;
using GingerVoxelSystem.Systems.Rendering;
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Terrain.Engine
{
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
            Modifications = modifications;
            Batch = batch;
            OnCompleted = onCompleted;
        }
    }

}
