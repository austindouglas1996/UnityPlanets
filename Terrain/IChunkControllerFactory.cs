using System.Threading;
using UnityEngine;

/// <summary>
/// Responsible for creating and returning a new chunk controller at a given coordinate.
/// Used by the chunk manager to spawn chunk GameObjects.
/// </summary>
public interface IChunkControllerFactory
{
    /// <summary>
    /// Create an instance of <see cref="IChunkGenerator"/>.
    /// </summary>
    /// <returns></returns>
    IChunkGenerator CreateGenerator();

    /// <summary>
    /// Creates a new chunk controller at the specified grid coordinate.
    /// </summary>
    /// <param name="coordinates">Chunk grid position.</param>
    /// <param name="config">Chunk configuration.</param>
    /// <param name="parent">The parent transform to attach the chunk to.</param>
    /// <returns>A new <see cref="ChunkController"/> instance.</returns>
    ChunkController CreateChunkController(Vector3Int coordinates, ChunkManager manager, IChunkConfiguration config, Transform parent, CancellationToken cancellationToken = default);
}
