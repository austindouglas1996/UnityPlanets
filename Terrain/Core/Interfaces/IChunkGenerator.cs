using Assets.Scripts.Terrain.Engine;
using GingerVoxelSystem.Systems.Generation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GingerVoxelSystem.Core
{
    /// <summary>
    /// GPU-driven pipeline for generating chunk surface data and meshes.
    /// Provides batched compute-dispatch operations for surface checks,
    /// density generation, and triangle extraction. 
    /// Returns GPU-resident buffers used directly by the render path.
    /// </summary>
    public interface IChunkGenerator : IDisposable
    {
        /// <summary>
        /// Runs a GPU shader to quickly check which chunks actually have a surface.
        /// </summary>
        /// <param name="keys">Chunk keys to check.</param>
        /// <returns>Array of chunk indexes that contain surface.</returns>
        void DispatchSurfaceCheck(IReadOnlyList<ChunkGenerationJob> jobs, Action<uint[]> onSuccess);

        /// <summary>
        /// Creates a chunk mesh on the GPU for the given chunk keys.
        /// Called the first time those chunks are loaded.
        /// </summary>
        /// <param name="keys">List of chunk keys to generate.</param>
        /// <returns>GPU data set for the generated chunks.</returns>
        void DispatchGeneration(DispatchJob job);

        /// <summary>
        /// Used for generators that operate on a schedule.
        /// </summary>
        void Update();

        /// <summary>
        /// Apply updated runtime/editor options to the generator (e.g., density params, biome tables).
        /// Implementations should re-upload constant/structured buffers and invalidate any caches
        /// so subsequent builds reflect the new settings.
        /// </summary>
        void UpdateOptions();

        /// <summary>
        /// Get the custom material used in generation.
        /// </summary>
        Material GetMaterial { get; }
    }
}