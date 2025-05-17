using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
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
    /// The last known follower position.
    /// </summary>
    public Vector3 LastFollowerPosition { get; protected set; } = new Vector3(999, 999, 999);

    /// <summary>
    /// The minimum amount of space that all previous active chunks should be disgarded as the follower
    /// has travelled too far for their positions to be relevant. 
    /// </summary>
    public float MinChangeForFullReset { get; protected set; } = 500f;

    /// <summary>
    /// The minimum distance the player should walk before agreeing to update the layout.
    /// </summary>
    public float MinChangeToUpdateLayout { get; set; } = 100f;

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

    public BoundsInt GetDesiredChunkBounds(Vector3 followerPosition)
    {  
        // Set the new position.
        this.LastFollowerPosition = followerPosition;

        int chunkSize = Configuration.ChunkSize;

        Vector3Int followerChunkPos = new Vector3Int(
            Mathf.FloorToInt(followerPosition.x / chunkSize),
            Mathf.FloorToInt(followerPosition.y / chunkSize),
            Mathf.FloorToInt(followerPosition.z / chunkSize)
        );

        int horizontalRange = 96; // adjust based on LOD/draw distance
        int verticalRange = 6;    // for flat terrain, Y can be narrow

        Vector3Int min = followerChunkPos - new Vector3Int(horizontalRange, verticalRange, horizontalRange);
        Vector3Int size = new Vector3Int(
            horizontalRange * 2 + 1,
            verticalRange * 2 + 1,
            horizontalRange * 2 + 1
        );

        return new BoundsInt(min, size);
    }


    /// <summary>
    /// Get a set of active that should be rendered but by streaming the results to reduce the process time.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <returns></returns>
    public virtual async IAsyncEnumerable<ChunkLayoutEntryInfo> StreamChunkLayoutUpdate(Vector3 followerPosition, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Has the follower travelled to far for us to keep up?
        bool FullReset = (followerPosition - LastFollowerPosition).sqrMagnitude > MinChangeForFullReset * MinChangeForFullReset;
        if (FullReset)
        {
            this.KnownAirChunks.Clear();
            this.KnownSurfaceChunks.Clear();
        }

        // Set the new position.
        this.LastFollowerPosition = followerPosition;

        using var queue = new BlockingCollection<ChunkLayoutEntryInfo>(new ConcurrentQueue<ChunkLayoutEntryInfo>());
        Task runner = Task.Run(() => 
        {
            try
            {
                GenerateChunkEntries(followerPosition, queue);
            }
            catch (OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                queue.CompleteAdding();
            }
        });

        while (!queue.IsCompleted && !token.IsCancellationRequested)
        {
            if (queue.TryTake(out var entry, 1, token))
            {
                yield return entry;
            }
            else
            {
                await Task.Yield();
            }
        }
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

    /// <summary>
    /// Generates the chunk layout update in a blocking collection so it can be streamed.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <param name="output"></param>
    private void GenerateChunkEntries(Vector3 followerPosition, BlockingCollection<ChunkLayoutEntryInfo> output)
    {
        Vector3Int followerChunkPos = new Vector3Int(
            Mathf.FloorToInt(followerPosition.x / this.Configuration.ChunkSize),
            Mathf.FloorToInt(followerPosition.y / this.Configuration.ChunkSize),
            Mathf.FloorToInt(followerPosition.z / this.Configuration.ChunkSize));

        List<Vector3Int> offsets = new();

        foreach (var x in this.Configuration.RenderDistanceInChunks.X.Values())
        {
            foreach (var z in this.Configuration.RenderDistanceInChunks.Z.Values())
            {
                foreach (var y in this.Configuration.RenderDistanceInChunks.Y.Values())
                {
                    offsets.Add(new Vector3Int(x, y, z));
                }
            }
        }

        offsets.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.x) + Mathf.Abs(a.y) + Mathf.Abs(a.z);
            int db = Mathf.Abs(b.x) + Mathf.Abs(b.y) + Mathf.Abs(b.z);
            return da.CompareTo(db);
        });

        HashSet<Vector3Int> activeChunks = new HashSet<Vector3Int>();
        foreach (var chunkOffset in offsets)
        {
            Vector3Int offset = followerChunkPos + chunkOffset;

            // Ignore air chunks as there is nothing to render.
            if (KnownAirChunks.Contains(offset))
                continue;

            int lod = GetRenderDetail(followerChunkPos, offset);

            // Unless this is a full reset, and the previous list contained
            // this chunk then let us keep rendering it.
            if (PreviousActiveChunks.Contains(offset) || KnownSurfaceChunks.Contains(offset))
            {
                activeChunks.Add(offset);
                output.Add(new ChunkLayoutEntryInfo(offset, lod));
            }
            else
            {
                switch (GetChunkResponse(followerChunkPos, offset))
                {
                    case ChunkResponse.Air:
                        KnownAirChunks.Add(offset);
                        break;
                    case ChunkResponse.Surface:
                        activeChunks.Add(offset);
                        KnownSurfaceChunks.Add(offset);
                        output.Add(new ChunkLayoutEntryInfo(offset, lod));
                        break;
                }
            }
        }

        // Remove the inactive chunks.
        HashSet<Vector3Int> toRemove = new HashSet<Vector3Int>(this.PreviousActiveChunks);
        toRemove.ExceptWith(activeChunks);
        foreach (var chunk in toRemove.OrderByDescending(c => Vector3.Distance(c, followerChunkPos)))
        {
            output.Add(new ChunkLayoutEntryInfo(chunk, -1, true));
        }

        // Set the collection to use the new collection.
        PreviousActiveChunks = activeChunks;
    }
}