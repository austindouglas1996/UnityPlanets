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
    Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, int lodIndex, IChunkConfiguration config, CancellationToken token = default);

    /// <summary>
    /// Applies a brush to modify an existing chunk’s density map.
    /// This is where terrain edits (add/remove) actually happen.
    /// </summary>
    /// <param name="data">The chunk data to modify.</param>
    /// <param name="config">Chunk configuration.</param>
    /// <param name="brush">Brush used to modify the chunk.</param>
    /// <param name="chunkPos">Chunk grid position.</param>
    /// <param name="addingOrSubtracting">True to add terrain, false to remove it.</param>
    /// <param name="token">Optional cancellation token.</param>
    Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default);

    /// <summary>
    /// Updates a chunk after it has been modified — usually to regenerate mesh data.
    /// Called after modifying the density map.
    /// </summary>
    /// <param name="data">Chunk data to update.</param>
    /// <param name="config">Chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default);

    /// <summary>
    /// Builds a mesh from the chunk's density data.
    /// Called after initial generation or terrain modification.
    /// </summary>
    /// <param name="chunk">The chunk to build a mesh for.</param>
    /// <param name="config">Chunk configuration.</param>
    /// <returns>The generated mesh.</returns>
    Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config);
}
