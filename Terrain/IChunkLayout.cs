using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// A structure class to help with a simple response from <see cref="GetActiveChunkCoordinates(Vector3)"/>.
/// </summary>
public class ChunkLayoutResponse
{
    public ChunkLayoutResponse(HashSet<ChunkLayoutEntryInfo> active, HashSet<Vector3Int> remove)
    {
        this.ActiveChunks = active;
        this.RemoveChunks = remove;
    }

    public HashSet<ChunkLayoutEntryInfo> ActiveChunks;
    public HashSet<Vector3Int> RemoveChunks;
}

/// <summary>
/// A structure to hold information on a chunks coordinates, and its LOD.
/// </summary>
public class ChunkLayoutEntryInfo
{
    public ChunkLayoutEntryInfo(Vector3Int coordinates, int lod, bool isStale = false) 
    {
        this.Coordinates = coordinates;
        this.LOD = lod;
        this.IsStale = isStale;
    }

    public Vector3Int Coordinates {  get; set; }
    public int LOD;
    public bool IsStale { get; private set; }
}

/// <summary>
/// Defines the layout and visibility logic for chunks in the terrain system.
/// Used to determine which chunks should be active and at what level of detail (LOD).
/// </summary>
public interface IChunkLayout
{
    /// <summary>
    /// A list of previously active chunks.
    /// </summary>
    HashSet<Vector3Int> PreviousActiveChunks { get; }

    /// <summary>
    /// Simple function on whether the layout should be updated.
    /// </summary>
    bool ShouldUpdateLayout(Vector3 followerPosition);

    /// <summary>
    /// Returns a list of chunk coordinates that should be active (loaded and rendered)
    /// based on the position of the follower in <see cref="ChunkManager"/>
    /// </summary>
    /// <param name="followerPosition">The world-space position of the object being followed.</param>
    /// <returns>A list of <see cref="Vector3Int"/> coordinates representing active chunks.</returns>
    Task<ChunkLayoutResponse> GetChunkLayoutUpdate(Vector3 followerPosition);

    /// <summary>
    /// Returns an enumerable of chunk coordinates that should be active, or removed.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <returns></returns>
    IAsyncEnumerable<ChunkLayoutEntryInfo> StreamChunkLayoutUpdate(Vector3 followerPosition);

    /// <summary>
    /// Determines the level of detail (LOD) that should be used for a given chunk
    /// based on its distance to the followed object.
    /// </summary>
    /// <param name="followerCoordinates">The chunk coordinate of the follower (e.g., player or camera).</param>
    /// <param name="chunkCoordinate">The chunk coordinate being evaluated.</param>
    /// <returns>An integer representing the LOD level (lower = higher detail).</returns>
    int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate);
}
