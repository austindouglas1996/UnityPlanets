using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines the layout and visibility logic for chunks in the terrain system.
/// Used to determine which chunks should be active and at what level of detail (LOD).
/// </summary>
public interface IChunkLayout
{
    /// <summary>
    /// Returns a list of chunk coordinates that should be active (loaded and rendered)
    /// based on the position of the follower in <see cref="ChunkManager"/>
    /// </summary>
    /// <param name="followerPosition">The world-space position of the object being followed.</param>
    /// <returns>A list of <see cref="Vector3Int"/> coordinates representing active chunks.</returns>
    List<Vector3Int> GetActiveChunkCoordinates(Vector3 followerPosition);

    /// <summary>
    /// Determines the level of detail (LOD) that should be used for a given chunk
    /// based on its distance to the followed object.
    /// </summary>
    /// <param name="followerCoordinates">The chunk coordinate of the follower (e.g., player or camera).</param>
    /// <param name="chunkCoordinate">The chunk coordinate being evaluated.</param>
    /// <returns>An integer representing the LOD level (lower = higher detail).</returns>
    int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate);
}
