using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Planet))]
public class PlanetRenderer : MonoBehaviour
{
    [Header("Rendering")]
    public float DistanceToUpdateChunks = 10f;
    public Material baseMaterial;
    public PlanetChunk ChunkPrefab;

    [Header("World")]
    public Planet Planet;
    public Universe Universe;
    public Transform Follower;

    private Vector3 lastKnownFollowerPosition;

    public List<PlanetChunk> ActiveChunks = new List<PlanetChunk>();
    private Dictionary<Vector3Int, PlanetChunk> ChunksCache = new Dictionary<Vector3Int, PlanetChunk>();

    private bool IsBusy = false;

    private void Start()
    {
        this.Planet = GetComponent<Planet>();
        this.Universe = GetComponent<Universe>();
    }

    private async void Update()
    {
        float viewerDistance = Vector3.Distance(Follower.position, lastKnownFollowerPosition);
        if (viewerDistance > DistanceToUpdateChunks)
        {
            lastKnownFollowerPosition = Follower.position;
            await UpdateActiveChunks();
        }
    }

    private async Task UpdateActiveChunks()
    {
        if (IsBusy) return;
        IsBusy = true;

        List<PlanetChunk> newActiveChunks = new List<PlanetChunk>();
        List<Vector3Int> chunksToLoad = new List<Vector3Int>();

        int chunkSize = Universe.PlanetChunkSize;       // 32
        float loadRadius = 64f;                          // Radius in world units
        Vector3 followerPosition = Follower.transform.position;

        // Convert follower position to chunk coordinates
        Vector3 chunkCenterPos = followerPosition / chunkSize;
        Vector3Int followerChunkCoord = new Vector3Int(
            Mathf.FloorToInt(chunkCenterPos.x),
            Mathf.FloorToInt(chunkCenterPos.y),
            Mathf.FloorToInt(chunkCenterPos.z)
        );

        // How many chunks to check in each direction
        int maxChunkOffset = Mathf.CeilToInt(loadRadius / chunkSize);

        for (int x = -maxChunkOffset; x <= maxChunkOffset; x++)
        {
            for (int y = -maxChunkOffset; y <= maxChunkOffset; y++)
            {
                for (int z = -maxChunkOffset; z <= maxChunkOffset; z++)
                {
                    Vector3Int chunkCoord = followerChunkCoord + new Vector3Int(x, y, z);

                    // Convert chunk coordinate back to world-space center
                    Vector3 chunkCenter = (Vector3)chunkCoord * chunkSize + Vector3.one * (chunkSize / 2f);

                    // Check if the chunk is within load radius of the follower
                    if (Vector3.Distance(chunkCenter, followerPosition) <= loadRadius)
                    {
                        chunksToLoad.Add(chunkCoord);
                    }
                }
            }
        }


        // Sort by distance to the player.
        chunksToLoad.Sort((a, b) =>
            Vector3.Distance(a, followerChunkCoord).CompareTo(Vector3.Distance(b, followerChunkCoord))
        );

        foreach (var chunkCoord in chunksToLoad)
        {
            PlanetChunk chunk;
            int rDetail = GetRenderDetail(chunkCoord);

            if (!ChunksCache.ContainsKey(chunkCoord))
            {
                Vector3 pos = new Vector3
                    (chunkCoord.x * Universe.PlanetChunkSize,
                    chunkCoord.y * Universe.PlanetChunkSize,
                    chunkCoord.z * Universe.PlanetChunkSize);

                chunk = Instantiate(ChunkPrefab, pos, Quaternion.identity, this.transform);
                await chunk.Generate(this.Planet, chunkCoord);

                ChunksCache.Add(chunkCoord, chunk);
            }
            else
            {
                chunk = ChunksCache[chunkCoord];
            }

            if (chunk.RenderDetail != rDetail)
            {
                chunk.RenderDetail = rDetail;
                await chunk.UpdateAsync();
            }

            chunk.SetVisible(true);
            newActiveChunks.Add(chunk);
        }

        foreach (var chunk in ActiveChunks.Except(newActiveChunks))
        {
            chunk.SetVisible(false);
        }

        ActiveChunks = newActiveChunks;
        IsBusy = false;
    }

    private int GetRenderDetail(Vector3Int coordinates)
    {
        return 1;
    }
}