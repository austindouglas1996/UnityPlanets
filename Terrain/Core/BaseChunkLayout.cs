using System.Linq;
using UnityEngine;

/// <summary>
/// A generic instance of <see cref="IChunkLayout"/> that fits most scenarios when creating a chunk layout
/// to help with reducing code reuse.
/// </summary>
public abstract class BaseChunkLayout : BaseChunkCore, IChunkLayout
{
    /// <summary>
    /// The set of LOD thresholds for chunk rendering.
    /// </summary>
    private int[] LODRings;

    /// <summary>
    /// Initializes a new instance of <see cref="BaseChunkLayout"/>
    /// </summary>
    /// <param name="configuration"></param>
    public BaseChunkLayout(IChunkConfiguration configuration)
        : base(configuration)
    {
        this.LODRings = this.Configuration.LODThresholds.ToArray();
    }

    /// <summary>
    /// Gets or sets the follower in the world.
    /// </summary>
    public Transform Follower { get; set; }

    /// <summary>
    /// Gets or sets the follower world position to be thread safe.
    /// </summary>
    public Vector3 FollowerWorldPosition
    {
        get { return followerWorldPosition; }
        set
        {
            followerWorldPosition = value;
            followerCoordinates = this.ToCoordinates(FollowerWorldPosition);
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
    /// Returns the chunk size for a given LOD level.
    /// </summary>
    /// <param name="lod"></param>
    /// <returns></returns>
    public int GetChunkSize(int lod)
    {
        return this.Configuration.DensityOptions.ChunkSize << lod;
    }

    /// <summary>
    /// Return a set of coordinates to world position.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    public Vector3 ToWorld(Vector3 coordinates)
    {
        int chunkSize = GetChunkSize(0);
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
    public Vector3Int ToCoordinates(Vector3 worldPositon)
    {
        int chunkSize = GetChunkSize(0);
        return new Vector3Int(
            Mathf.FloorToInt(worldPositon.x / chunkSize),
            Mathf.FloorToInt(worldPositon.y / chunkSize),
            Mathf.FloorToInt(worldPositon.z / chunkSize));
    }

    /// <summary>
    /// Retrieve the <see cref="Bounds"/> for a given <see cref="ChunkKey"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Bounds GetBounds(ChunkKey key)
    {
        return GetBounds(key.Coordinates, key.LODIndex);
    }

    /// <summary>
    /// Retrieve the <see cref="Bounds"/> for a given <see cref="ChunkKey"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Bounds GetBounds(Vector3Int coordinates, int lodIndex)
    {
        int chunkSize = GetChunkSize(lodIndex);

        Vector3 worldPos = new Vector3(
             coordinates.x * chunkSize,
             coordinates.y * chunkSize,
             coordinates.z * chunkSize);

        Bounds bounds = new Bounds
        {
            center = worldPos + new Vector3(chunkSize, chunkSize, chunkSize) * 0.5f,
            size = new Vector3(chunkSize, chunkSize, chunkSize)
        };

        return bounds;
    }

    /// <summary>
    /// Retrieve a set of coordinates based on a <see cref="Bounds"/> object.
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="lodIndex"></param>
    /// <returns></returns>
    public Vector3Int BoundsToCoordinates(Bounds bounds, int lodIndex)
    {
        int chunkSize = GetChunkSize(lodIndex);
        Vector3 pos = bounds.min;

        return new Vector3Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.y / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize));
    }

    /// <summary>
    /// Retrieve the expected chunk LOD level for a given coordinate.
    /// </summary>
    /// <param name="chunkCoordinates"></param>
    /// <returns></returns>
    public int GetLODForChunk(Vector3Int coord)
    {
        int chunkSize = GetChunkSize(0);

        Vector3 worldMin = coord * chunkSize;
        Vector3 worldMax = worldMin + new Vector3(chunkSize, chunkSize, chunkSize);

        Vector3 player = FollowerWorldPosition;

        // Clamp player position to chunk AABB
        float px = Mathf.Clamp(player.x, worldMin.x, worldMax.x);
        float pz = Mathf.Clamp(player.z, worldMin.z, worldMax.z);

        float dx = Mathf.Abs(player.x - px);
        float dz = Mathf.Abs(player.z - pz);

        // Convert distance to LOD0 chunk units
        float maxDist = Mathf.Max(dx, dz);
        int ring = Mathf.CeilToInt(maxDist / chunkSize);

        return DesiredLodFromRings(ring);
    }

    /// <summary>
    /// Determine the best LOD ring to use based on the distance.
    /// </summary>
    /// <param name="dChunks0"></param>
    /// <param name="rings"></param>
    /// <returns></returns>
    private int DesiredLodFromRings(int dChunks0)
    {
        for (int i = 0; i < LODRings.Length; i++)
        {
            if (dChunks0 < LODRings[i])
                return i;
        }

        return LODRings.Count() - 1;
    }
}