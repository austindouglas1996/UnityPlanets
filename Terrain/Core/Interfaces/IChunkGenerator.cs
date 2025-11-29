using System;
using System.Collections.Generic;
using UnityEngine;
using UnityTerrainGenerator.Systems.Generation;
using UnityTerrainGenerator.Systems.Rendering;

namespace UnityTerrainGenerator.Core
{
    /// <summary>
    /// Handles chunk generation, mesh building, and terrain modifications.
    /// Used by the chunk manager to build and update chunks based on data and brush input.
    /// </summary>
    public interface IChunkGenerator : IDisposable
    {
        /// <summary>
        /// Runs a GPU shader to quickly check which chunks actually have a surface.
        /// </summary>
        /// <param name="keys">Chunk keys to check.</param>
        /// <returns>Array of chunk indexes that contain surface.</returns>
        void DispatchSurfaceChecks(IReadOnlyList<ChunkGenerationJob> jobs, Action<uint[]> output);

        /// <summary>
        /// Creates a chunk mesh on the GPU for the given chunk keys.
        /// Called the first time those chunks are loaded.
        /// </summary>
        /// <param name="keys">List of chunk keys to generate.</param>
        /// <returns>GPU data set for the generated chunks.</returns>
        void DispatchGeneration(ChunkKey?[] keys, int count, Dictionary<int, ChunkKey?> modifications, Action<ChunkRenderBatch> output, ChunkRenderBatch existingBatch = null);

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