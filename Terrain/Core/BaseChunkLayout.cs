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
    public Vector3 ToWorld(ChunkKey key)
    {
        int chunkSize = GetChunkSize(key.LODIndex);
        return new Vector3(key.Coordinates.x * chunkSize, key.Coordinates.y * chunkSize, key.Coordinates.z * chunkSize);
    }

    /// <summary>
    /// Return a world position in world coordinates.
    /// </summary>
    /// <param name="world"></param>
    /// <returns></returns>
    public Vector3Int ToCoordinates(Vector3 worldPositon, int lodIndex)
    {
        int chunkSize = GetChunkSize(lodIndex);
        return new Vector3Int(
            Mathf.FloorToInt(worldPositon.x / chunkSize),
            Mathf.FloorToInt(worldPositon.y / chunkSize),
            Mathf.FloorToInt(worldPositon.z / chunkSize));
    }

    /// <summary>
    /// Retrieve the expected chunk LOD level for a given coordinate.
    /// </summary>
    /// <param name="chunkCoordinates"></param>
    /// <returns></returns>
    public int GetLODForChunk(ChunkKey key)
    {
        int baseChunkSize = Configuration.DensityOptions.ChunkSize;
        int chunkSize = GetChunkSize(key.LODIndex);

        // Compute chunk world bounds (no Bounds)
        int chunkMinX = key.Coordinates.x * chunkSize;
        int chunkMaxX = chunkMinX + chunkSize;
        int chunkMinZ = key.Coordinates.z * chunkSize;
        int chunkMaxZ = chunkMinZ + chunkSize;

        float px = FollowerWorldPosition.x;
        float pz = FollowerWorldPosition.z;

        int dx = DistToInterval(px, chunkMinX, chunkMaxX);
        int dz = DistToInterval(pz, chunkMinZ, chunkMaxZ);

        int chebDist = Mathf.CeilToInt(Mathf.Max(dx, dz) / (float)baseChunkSize);
        return DesiredLodFromRings(chebDist);
    }

    /// <summary>
    /// Determine the best LOD ring to use based on the distance.
    /// </summary>
    /// <param name="dChunks0"></param>
    /// <param name="rings"></param>
    /// <returns></returns>
    private int DesiredLodFromRings(int dChunks0)
    {
        // rings[L] = max distance (in LOD0 chunks) where LOD == L
        for (int L = 0; L < LODRings.Length; L++)
            if (dChunks0 <= LODRings[L]) 
                return L;
        return LODRings.Length - 1;
    }

    /// <summary>
    /// Returns the distance between two variables.
    /// </summary>
    /// <param name="p"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private static int DistToInterval(float p, float a, float b)
    {
        if (p < a) return Mathf.CeilToInt(a - p);
        if (p > b) return Mathf.CeilToInt(p - b);
        return 0;
    }
}