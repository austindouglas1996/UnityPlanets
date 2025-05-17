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
    /// Returns the <see cref="BoundsInt"/> of the last collection of active chunks around the follower.
    /// using this saves space on the CPU and storage for faster rendering speeds.
    /// </summary>
    public BoundsInt PreviousActiveChunks { get; private set; }

    /// <summary>
    /// Gets or sets the follower in the world.
    /// </summary>
    public Transform Follower { get; set; }

    /// <summary>
    /// Gets or sets the follower world position to be thread safe.
    /// </summary>
    public Vector3 FollowerWorldPosition { get; set; }

    /// <summary>
    /// Gets the chunk coordinates of the follower.
    /// </summary>
    public Vector3Int FollowerCoordinates
    {
        get
        {
            return new Vector3Int(
            Mathf.FloorToInt(FollowerWorldPosition.x / Configuration.ChunkSize),
            Mathf.FloorToInt(FollowerWorldPosition.y / Configuration.ChunkSize),
            Mathf.FloorToInt(FollowerWorldPosition.z / Configuration.ChunkSize));
        }
    }

    /// <summary>
    /// The last known follower position.
    /// </summary>
    public Vector3 LastFollowerPosition { get; protected set; } = new Vector3(999, 999, 999);

    /// <summary>
    /// The minimum distance the player should walk before agreeing to update the layout.
    /// </summary>
    public float MinChangeToUpdateLayout { get; set; } = 100f;

    /// <summary>
    /// Returns whether the player has travelled far enough we should update the layout.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <returns></returns>
    public virtual bool ShouldUpdateLayout()
    {
        float viewerDistance = Vector3.Distance(this.FollowerWorldPosition, LastFollowerPosition);
        if (viewerDistance > MinChangeToUpdateLayout)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retrieve the active chunks around the player that should be rendered.
    /// </summary>
    /// <returns></returns>
    public BoundsInt GetActiveChunksAroundFollower(bool initial = false)
    {
        // Set the new position.
        this.LastFollowerPosition = this.FollowerWorldPosition;

        int chunkSize = Configuration.ChunkSize;

        int horizontalRange = 96; // adjust based on LOD/draw distance
        int verticalRange = 6;    // for flat terrain, Y can be narrow

        Vector3Int min = this.FollowerCoordinates - new Vector3Int(horizontalRange, verticalRange, horizontalRange);
        Vector3Int size = new Vector3Int(
            horizontalRange * 2 + 1,
            verticalRange * 2 + 1,
            horizontalRange * 2 + 1
        );

        return new BoundsInt(min, size);
    }

    /// <summary>
    /// Get the LOD for a given chunk based on the distance from a given follower.
    /// </summary>
    /// <param name="followerCoordinates"></param>
    /// <param name="chunkCoordinate"></param>
    /// <returns></returns>
    public int GetRenderDetail(Vector3Int chunkCoordinate)
    {
        Vector3Int coord = this.FollowerCoordinates;
        int dx = Mathf.Abs(chunkCoordinate.x - coord.x);
        int dz = Mathf.Abs(chunkCoordinate.z - coord.z);

        int distance = Mathf.Max(dx, dz);

        // Each tier is 12 chunks wide.
        int lod = distance / 24;

        // Clamp to a max LOD of 5, anything over 5 does not render.
        return Mathf.Min(lod, 5);
    }
}