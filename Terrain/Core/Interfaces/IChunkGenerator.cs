using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
    uint[] DispatchSurface(IReadOnlyList<ChunkGenerationJob> jobs);

    /// <summary>
    /// Creates a chunk mesh on the GPU for the given chunk keys.
    /// Called the first time those chunks are loaded.
    /// </summary>
    /// <param name="keys">List of chunk keys to generate.</param>
    /// <returns>GPU data set for the generated chunks.</returns>
    ChunkRenderBatch DispatchGeneration(IReadOnlyList<ChunkKey> keys);
}
