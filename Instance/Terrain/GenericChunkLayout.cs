using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A generic instance of <see cref="IChunkLayout"/> that fits most scenarios when creating a chunk layout
/// to help with reducing code reuse.
/// </summary>
public abstract class GenericChunkLayout : IChunkLayout
{
    /// <summary>
    /// Simple enum for deciding on how a chunk should be handled.
    /// </summary>
    protected enum ChunkResponse
    {
        Surface,
        Air
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GenericChunkLayout"/>
    /// </summary>
    /// <param name="configuration"></param>
    public GenericChunkLayout(IChunkConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Configuration used for chunk generation.
    /// </summary>
    public IChunkConfiguration Configuration;

    /// <summary>
    /// A collection of last active chunks that were given. Helps with only retrieving
    /// the difference on chunks needing to be rendered/destroyed.
    /// </summary>
    public HashSet<Vector3Int> PreviousActiveChunks { get; protected set; } = new HashSet<Vector3Int>();

    /// <summary>
    /// A collection of known air chunks for easy reference on knowing
    /// not to select this chunk for rendering.
    /// </summary>
    public HashSet<Vector3Int> KnownAirChunks = new HashSet<Vector3Int>();

    /// <summary>
    /// A collection of known surface chunks for easy reference on grabbing
    /// known chunk positions to help with deciding.
    /// </summary>
    public HashSet<Vector3Int> KnownSurfaceChunks = new HashSet<Vector3Int>();

    /// <summary>
    /// The amount of chunks between negative/positive that should be rendered from the 
    /// follower chunk position. 
    /// 
    /// Example given 6.
    /// -6 < X < 6
    /// -6 < Y < 6
    /// -6 < Z < 6
    /// </summary>
    public abstract int ChunkRenderDistanceInChunks { get; protected set; }

    /// <summary>
    /// The last known follower position.
    /// </summary>
    public Vector3 LastFollowerPosition { get; protected set; } = new Vector3(999, 999, 999);

    /// <summary>
    /// The minimum amount of space that all previous active chunks should be disgarded as the follower
    /// has travelled too far for their positions to be relevant. 
    /// </summary>
    public float MinChangeForFullReset { get; protected set; } = 200f;

    /// <summary>
    /// The minimum distance the player should walk before agreeing to update the layout.
    /// </summary>
    public float MinChangeToUpdateLayout { get; set; } = 20f;

    /// <summary>
    /// Returns whether the player has travelled far enough we should update the layout.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <returns></returns>
    public virtual bool ShouldUpdateLayout(Vector3 followerPosition)
    {
        float viewerDistance = Vector3.Distance(followerPosition, LastFollowerPosition);
        if (viewerDistance > MinChangeToUpdateLayout)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get a set of active chunks that should be rendered.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <returns></returns>
    public virtual async Task<ChunkLayoutResponse> GetChunkLayoutUpdate(Vector3 followerPosition)
    {
        return await Task.Run(() =>
        {
            // Has the follower travelled to far for us to keep up?
            bool FullReset = (followerPosition - LastFollowerPosition).sqrMagnitude > MinChangeForFullReset * MinChangeForFullReset;

            // Memory management.
            if (FullReset)
            {
                this.KnownAirChunks.Clear();
                this.KnownSurfaceChunks.Clear();
            }

            // Set the new position.
            this.LastFollowerPosition = followerPosition;

            int chunkSize = this.Configuration.ChunkSize;
            int maxChunkOffset = ChunkRenderDistanceInChunks;

            Vector3Int followerChunkPos = new Vector3Int(
                Mathf.FloorToInt(followerPosition.x / chunkSize),
                Mathf.FloorToInt(followerPosition.y / chunkSize),
                Mathf.FloorToInt(followerPosition.z / chunkSize));

            List<ChunkLayoutEntryInfo> chunksToLoad = new();
            for (int x = -maxChunkOffset; x <= maxChunkOffset; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    for (int z = -maxChunkOffset; z <= maxChunkOffset; z++)
                    {
                        Vector3Int offset = followerChunkPos + new Vector3Int(x, y, z);

                        // Ignore air chunks as there is nothing to render.
                        if (KnownAirChunks.Contains(offset))
                            continue;

                        int lod = GetRenderDetail(followerChunkPos, offset);

                        // Unless this is a full reset, and the previous list contained
                        // this chunk then let us keep rendering it.
                        if (!FullReset && PreviousActiveChunks.Contains(offset))
                        {
                            chunksToLoad.Add(new ChunkLayoutEntryInfo(offset, lod));
                        }

                        // This chunk has not been seen before. Let's get some
                        // information on it. 
                        switch (GetChunkResponse(followerChunkPos, offset))
                        {
                            case ChunkResponse.Air:
                                KnownAirChunks.Add(offset);
                                break;
                            case ChunkResponse.Surface:
                                KnownSurfaceChunks.Add(offset);
                                chunksToLoad.Add(new ChunkLayoutEntryInfo(offset, lod));
                                break;
                        }   
                    }
                }
            }

            // Sort by distance.
            chunksToLoad.Sort((a, b) =>
                Vector3.Distance(a.Coordinates, followerChunkPos).CompareTo(Vector3.Distance(b.Coordinates, followerChunkPos)));

            HashSet<Vector3Int> newChunkCoords = new HashSet<Vector3Int>(
                chunksToLoad.Select(entry => entry.Coordinates));

            HashSet<Vector3Int> toRemove = new HashSet<Vector3Int>(this.PreviousActiveChunks);
            toRemove.ExceptWith(newChunkCoords);

            this.PreviousActiveChunks = newChunkCoords;

            return new ChunkLayoutResponse(chunksToLoad.ToHashSet(), toRemove);
        });
    }

    /// <summary>
    /// Get the LOD for a given chunk based on the distance from a given follower.
    /// </summary>
    /// <param name="followerCoordinates"></param>
    /// <param name="chunkCoordinate"></param>
    /// <returns></returns>
    public int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate)
    {
        int dx = Mathf.Abs(chunkCoordinate.x - followerCoordinates.x);
        int dz = Mathf.Abs(chunkCoordinate.z - followerCoordinates.z);

        int distance = Mathf.Max(dx, dz);

        // Each tier is 12 chunks wide.
        int lod = distance / 24;

        // Clamp to a max LOD of 5, anything over 5 does not render.
        return Mathf.Min(lod, 5);
    }

    /// <summary>
    /// Return the type of chunk at this coordinate is.
    /// </summary>
    /// <param name="followerCoordinates">The follower position in case needed for determining if chunk should be rendered.</param>
    /// <param name="coordinates">The coordinates of the chunk.</param>
    /// <returns><see cref="ChunkResponse"/></returns>
    protected abstract ChunkResponse GetChunkResponse(Vector3Int followerCoordinates, Vector3Int coordinates);
}