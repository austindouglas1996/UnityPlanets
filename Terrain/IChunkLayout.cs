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
    /// A list of previously active chunks.
    /// </summary>
    BoundsInt PreviousActiveChunks { get; }

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
    /// Retrieves the chunks around the follower based on chunk configuration using a BoundsInt to save on
    /// disk and memory space for fast collection speeds.
    /// </summary>
    /// <returns></returns>
    BoundsInt GetActiveChunksAroundFollower(bool initial = false);

    /// <summary>
    /// Determines the level of detail (LOD) that should be used for a given chunk
    /// based on its distance to the followed object.
    /// </summary>
    /// <param name="chunkCoordinate">The chunk coordinate being evaluated.</param>
    /// <returns>An integer representing the LOD level (lower = higher detail).</returns>
    int GetRenderDetail(Vector3Int chunkCoordinate);
}
