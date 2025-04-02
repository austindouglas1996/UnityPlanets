using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Planet : MonoBehaviour
{
    [Header("Components")]
    public Universe Universe;
    public PlanetChunk ChunkPrefab;

    [Header("Generation")]
    public int Radius = 128;

    public DensityMapOptions MapOptions;

    [Tooltip("Gives the base height of the surface of the planet. This serves as the starting point of the color scale. NOTE: Anything below it will be treated as zero.")]
    public float StartSurfaceColorRadius = 220;

    [Tooltip("Gives the max height of the surface of the planet. This serves as the top of the color scale. NOTE: Anything above it will be treated as the max.")]
    public float EndSurfaceColorRadius = 256;

    [Tooltip("Automatically handle the start and end of the surface colors. This is useful when testing different height differences and need to see the color changes.")]
    public bool AutoHandleSurfaceColorRadius = true;

    [Header("Rendering")]
    [Tooltip("How far a given chunk can be that it will be rendered on screen. Details will automatically be adjusted on distance.")]
    public float ChunkRenderDistance = 400;

    [Tooltip("How far the follower needs to be travel before we update the active chunks.")]
    public float TravelDistanceToUpdateChunks = 10f;

    /// <summary>
    /// Last known position the follower was seen at.
    /// </summary>
    private Vector3 lastKnownFollowerPosition;

    /// <summary>
    /// Contains a list of active chunks that are currently active.
    /// </summary>
    public List<PlanetChunk> ActiveChunks = new List<PlanetChunk>();

    /// <summary>
    /// Contains a list of seen chunks.
    /// </summary>
    private Dictionary<Vector3Int, PlanetChunk> ChunksCache = new Dictionary<Vector3Int, PlanetChunk>();

    /// <summary>
    /// Helps with stopping functions from being called multiple times. Sometimes Unity and async functions do not get along.
    /// </summary>
    private bool IsBusy = false;

    public Vector3 Center
    {
        get { return new Vector3(0,0,0); }
    }

    public void Rebuild()
    {
        while(this.transform.childCount != 0)
        {
            foreach (Transform go in this.transform)
            {
                DestroyImmediate(go.gameObject);
            }
        }

        ActiveChunks.Clear();
        ChunksCache.Clear();

        lastKnownFollowerPosition = new Vector3(999, 999, 999);
        Universe.Follower.transform.position = new Vector3(this.transform.position.x, Radius, this.transform.position.z);

        this.IsBusy = false;

        UpdateActiveChunks();
    }

    private void Start()
    {
        lastKnownFollowerPosition = new Vector3(999,999,999);
        this.Universe = GetComponent<Universe>();

        Universe.Follower.transform.position = new Vector3(this.transform.position.x, Radius, this.transform.position.z);
    }

    private async void Update()
    {
        if (IsFollowerOutsideOfRange())
        {
            await UpdateActiveChunks();
        }
    }

    /// <summary>
    /// Update the active chunks on the planet.
    /// </summary>
    /// <returns></returns>
    private async Task UpdateActiveChunks()
    {
        // Don't let this function run if we are already running.
        if (IsBusy) return;
        IsBusy = true;

        // In case sizes changed.
        if (AutoHandleSurfaceColorRadius)
        {
            this.UpdateSurfaceRadius();
        }

        List<PlanetChunk> newActiveChunks = new List<PlanetChunk>();

        foreach (var chunkCoord in GetChunksAroundFollower(ChunkRenderDistance))
        {
            PlanetChunk chunk = await GetOrInstantiateChunk(chunkCoord);

            // Check the render detail for this chunk. Update the chunk if incorrect.
            int renderDetail = GetRenderDetail(chunkCoord);
            if (chunk.RenderDetail != renderDetail)
            {
                chunk.RenderDetail = renderDetail;
                await chunk.UpdateAsync();
            }

            // Set the chunk as visible and add to the collection.
            chunk.SetVisible(true);
            newActiveChunks.Add(chunk);
        }

        // Remove chunks that are no longer in view, but exclude those
        // in the active chunks list.
        foreach (var chunk in ActiveChunks.Except(newActiveChunks))
        {
            if (chunk != null)
                chunk.SetVisible(false);
        }

        ActiveChunks = newActiveChunks;
        IsBusy = false;
    }

    /// <summary>
    /// Automatically adjust the surface radius sizes.
    /// </summary>
    private void UpdateSurfaceRadius()
    {
        this.StartSurfaceColorRadius = (this.Radius - 40);
        this.EndSurfaceColorRadius = (this.Radius - 20);
    }

    /// <summary>
    /// Get the render detail for a specific chunk based on its coordinates. Render detail will return a
    /// higher value for being closer to the follower.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    private int GetRenderDetail(Vector3Int coordinates)
    {
        return 1;
    }

    /// <summary>
    /// Returns whether the follower has walked far enough away from their last position that we should update the list of active chunks.
    /// </summary>
    /// <returns></returns>
    private bool IsFollowerOutsideOfRange()
    {
        float viewerDistance = Vector3.Distance(Universe.Follower.position, lastKnownFollowerPosition);
        if (viewerDistance > TravelDistanceToUpdateChunks)
        {
            lastKnownFollowerPosition = Universe.Follower.position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retrieve a list of <see cref="PlanetChunk"/> around the follower position.
    /// </summary>
    /// <param name="followerPosition"></param>
    /// <param name="loadRadius"></param>
    /// <returns></returns>
    private List<Vector3Int> GetChunksAroundFollower(float loadRadius)
    {
        int chunkSize = Universe.PlanetChunkSize;
        int maxChunkOffset = Mathf.CeilToInt(loadRadius / chunkSize);

        // Convert world position to chunk coordinate
        Vector3Int centerChunkCoord = WorldToChunkCoord(Universe.Follower.position);

        List<Vector3Int> chunksToLoad = new();

        for (int x = -maxChunkOffset; x <= maxChunkOffset; x++)
        {
            for (int y = -maxChunkOffset; y <= maxChunkOffset; y++)
            {
                for (int z = -maxChunkOffset; z <= maxChunkOffset; z++)
                {
                    Vector3Int offset = new(x, y, z);
                    Vector3Int chunkCoord = centerChunkCoord + offset;

                    // Get the chunk's world-space center
                    Vector3 chunkCenter = (Vector3)chunkCoord * chunkSize + Vector3.one * (chunkSize / 2f);

                    // Check both the follower load radius and the planet boundary.
                    if (Vector3.Distance(chunkCenter, Universe.Follower.position) <= loadRadius &&
                        Vector3.Distance(chunkCenter, Center) <= Radius + 20)
                    {
                        chunksToLoad.Add(chunkCoord);
                    }
                }
            }
        }

        // Sort by distance.
        chunksToLoad.Sort((a, b) =>
            Vector3.Distance(a, centerChunkCoord).CompareTo(Vector3.Distance(b, centerChunkCoord))
        );


        return chunksToLoad;
    }

    /// <summary>
    /// Convert world position into chunk coordinates.
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    private Vector3Int WorldToChunkCoord(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / Universe.PlanetChunkSize),
            Mathf.FloorToInt(worldPos.y / Universe.PlanetChunkSize),
            Mathf.FloorToInt(worldPos.z / Universe.PlanetChunkSize)
        );
    }

    /// <summary>
    /// Check whether a chuck is within a given range of the follower.
    /// </summary>
    /// <param name="chunkCoord"></param>
    /// <param name="maxDist"></param>
    /// <returns></returns>
    private bool IsChunkInRangeOfFollwer(Vector3Int chunkCoord, float maxDist)
    {
        Vector3 chunkCenter = (Vector3)chunkCoord * Universe.PlanetChunkSize + Vector3.one * (Universe.PlanetChunkSize / 2f);
        return Vector3.Distance(chunkCenter, Center) <= maxDist;
    }

    /// <summary>
    /// Retrieve or, initalize a new chunk instance based on coordinates.
    /// </summary>
    /// <param name="chunkCoord"></param>
    /// <returns></returns>
    private async Task<PlanetChunk> GetOrInstantiateChunk(Vector3Int chunkCoord)
    {
        PlanetChunk chunk;

        if (!ChunksCache.ContainsKey(chunkCoord))
        {
            chunk = await InstantiateChunk(chunkCoord);
            ChunksCache.Add(chunkCoord, chunk);
        }
        else
        {
            chunk = ChunksCache[chunkCoord];
            if (chunk.IsDestroyed())
            {
                chunk = await InstantiateChunk(chunkCoord);
            }
        }

        return chunk;
    }

    /// <summary>
    /// Initialize a new chunk on a planet.
    /// </summary>
    /// <param name="chunkCoord"></param>
    /// <returns></returns>
    private async Task<PlanetChunk> InstantiateChunk(Vector3Int chunkCoord)
    {
        PlanetChunk newChunk;

        Vector3 pos = new Vector3(
            chunkCoord.x * Universe.PlanetChunkSize,
            chunkCoord.y * Universe.PlanetChunkSize,
            chunkCoord.z * Universe.PlanetChunkSize);

        newChunk = Instantiate(ChunkPrefab, pos, Quaternion.identity, this.transform);
        await newChunk.Generate(this, chunkCoord);

        return newChunk;
    }
}
