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
    public float SurfaceRoughness = 0.05f;

    [Header("Noise")]
    public float Threshold = 0.5f;
    public int Octaves = 12;
    public float Noise = 0.1f;

    [Header("Rendering")]
    public float DistanceToUpdateChunks = 10f;
    public Material BaseMaterial;

    public Vector3 Center;

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

    [SerializeField]
    private Gradient purpleSwirlGradientS = new Gradient
    {
        colorKeys = new GradientColorKey[]
    {
        new GradientColorKey(new Color(0.2f, 0f, 0.4f), 0f),    // Deep purple
        new GradientColorKey(new Color(0.6f, 0f, 0.8f), 0.4f),  // Lighter violet
        new GradientColorKey(new Color(0.9f, 0.6f, 1f), 0.7f),  // Soft lavender
        new GradientColorKey(Color.white, 1f)                  // White highlight
    },
        alphaKeys = new GradientAlphaKey[]
    {
        new GradientAlphaKey(1f, 0f),
        new GradientAlphaKey(1f, 1f)
    }
    };


    /// <summary>
    /// Generate a new density map for a set chunk coordinates.
    /// </summary>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    public PlanetMapData GenerateMap(Vector3Int coordinates)
    {
        PlanetMapData newMap = new PlanetMapData();
        newMap.DensityMap = MarchingCubes.GenerateRoundMap(Universe.PlanetChunkSize, coordinates, Center, Radius);

        int width = newMap.DensityMap.GetLength(0);
        int height = newMap.DensityMap.GetLength(1);
        Color[] colorMap = new Color[width * height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = new Vector3(
                    coordinates.x * Universe.PlanetChunkSize + x,
                    coordinates.y * Universe.PlanetChunkSize + y,
                    coordinates.z * Universe.PlanetChunkSize); // or use z=0 if not needed

                float noiseValue = Perlin.Fbm(worldPos.x * Noise, worldPos.y * Noise, worldPos.z * Noise, Octaves);

                float normalized = Mathf.InverseLerp(-1f, 1f, noiseValue);
                colorMap[y * width + x] = purpleSwirlGradientS.Evaluate(normalized);
            }
        }

        newMap.ColorMap = colorMap;


        return newMap;
    }

    private void Start()
    {
        lastKnownFollowerPosition = new Vector3(999,999,999);
        this.Universe = GetComponent<Universe>();
    }

    private async void Update()
    {
        // Update the list of active chunks if the player has walked away enough.
        float viewerDistance = Vector3.Distance(Universe.Follower.position, lastKnownFollowerPosition);
        if (viewerDistance > DistanceToUpdateChunks)
        {
            lastKnownFollowerPosition = Universe.Follower.position;
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

        List<PlanetChunk> newActiveChunks = new List<PlanetChunk>();

        foreach (var chunkCoord in GetChunksAroundFollower(248f))
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
            chunk.SetVisible(false);
        }

        ActiveChunks = newActiveChunks;
        IsBusy = false;
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
