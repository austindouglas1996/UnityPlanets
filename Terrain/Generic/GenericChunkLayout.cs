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
    /// Configuration used for chunk generation.
    /// </summary>
    private IChunkConfiguration Configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="GenericChunkLayout"/>
    /// </summary>
    /// <param name="configuration"></param>
    public GenericChunkLayout(IChunkConfiguration configuration)
    {
        this.Configuration = configuration;
    }

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
    public Vector3 FollowerWorldPosition
    {
        get {  return followerWorldPosition; }
        set
        {
            followerWorldPosition = value;
            followerCoordinates = this.ToCoordinates(FollowerWorldPosition, 0);
        }
    }
    private Vector3 followerWorldPosition;

    /// <summary>
    /// Gets the chunk coordinates of the follower.
    /// </summary>
    public Vector3Int FollowerCoordinates
    {
        get { return followerCoordinates; }
    }
    private Vector3Int followerCoordinates;

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
            this.LastFollowerPosition = this.FollowerWorldPosition;
            return true;
        }

        return false;
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

    /// <summary>
    /// Return a set of coordinates to world position.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    public Vector3 ToWorld(Vector3Int coordinates, int lodIndex)
    {
        int chunkSize = Configuration.DensityOptions.ChunkSize << lodIndex;
        return new Vector3(
            coordinates.x * chunkSize,
            coordinates.y * chunkSize,
            coordinates.z * chunkSize);
    }

    /// <summary>
    /// Return a world position in world coordinates.
    /// </summary>
    /// <param name="world"></param>
    /// <returns></returns>
    public Vector3Int ToCoordinates(Vector3 world, int lodIndex)
    {
        int chunkSize = Configuration.DensityOptions.ChunkSize << lodIndex;
        return new Vector3Int(
            Mathf.FloorToInt(world.x / chunkSize),
            Mathf.FloorToInt(world.y / chunkSize),
            Mathf.FloorToInt(world.z / chunkSize));
    }
}