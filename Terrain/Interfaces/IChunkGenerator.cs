using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Handles chunk generation, mesh building, and terrain modifications.
/// Used by the chunk manager to build and update chunks based on data and brush input.
/// </summary>
public interface IChunkGenerator
{
    /// <summary>
    /// Generates a new chunk at the given coordinates using the config provided.
    /// This is called when a chunk is loaded for the first time.
    /// </summary>
    /// <param name="coordinates">Chunk grid position.</param>
    /// <param name="config">Chunk settings/configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>Newly generated chunk data.</returns>
    Dictionary<Vector3Int, MeshData> DispatchGeneration(List<ChunkContext> chunkContexts, CancellationToken token);
}
