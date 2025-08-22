using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

/// <summary>
/// Defines the layout and visibility logic for chunks in the terrain system.
/// Used to determine which chunks should be active and at what level of detail (LOD).
/// </summary>
public interface IChunkLayout
{
    /// <summary>
    /// Gets or sets the follower.
    /// </summary>
    Transform Follower { get; set; }

    /// <summary>
    /// Gets or sets the world position of the follower in a thread-safe way.
    /// </summary>
    Vector3 FollowerWorldPosition { get; set; }

    /// <summary>
    /// Gets the follower position in world coordinates.
    /// </summary>
    Vector3Int FollowerCoordinates { get; }

    /// <summary>
    /// Simple function on whether the layout should be updated.
    /// </summary>
    bool ShouldUpdateLayout();

    /// <summary>
    /// Gets the chunk size for a given LOD level.
    /// </summary>
    /// <param name="lod"></param>
    /// <returns></returns>
    int GetChunkSize(int lod);

    /// <summary>
    /// Convert a vector into world position.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    Vector3 ToWorld(ChunkKey key);

    /// <summary>
    /// Convert a vector into coordinates based on chunkSize.
    /// </summary>
    /// <param name="world"></param>
    /// <returns></returns>
    Vector3Int ToCoordinates(Vector3 worldPosition, int lodIndex);

    /// <summary>
    /// Get the desired LOD level for a given chunk.
    /// </summary>
    /// <param name="chunkCoordinates"></param>
    /// <returns></returns>
    public int GetLODForChunk(ChunkKey key);
}
