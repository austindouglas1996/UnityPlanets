using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(MapGenerator))]    
public class EndlessTerrain : MonoBehaviour
{
    public Transform Viewer;
    public float ViewerMoveThresholdForUpdate = 10f;
    public Vector2 ChunksToLoad = new Vector2(5, 5);

    private Vector3 lastKnownViewerPosition;

    private MapGenerator mapGenerator;
    private Dictionary<Vector2, TerrainChunk> terrainChunks = new Dictionary<Vector2, TerrainChunk>();
    private List<TerrainChunk> activeChunks = new List<TerrainChunk>();

    private bool generating = false;

    public bool forceRegen = false;

    private async void OnValidate()
    {
        if (forceRegen)
        {
            await UpdateActiveChunks();

            forceRegen = false;
        }
    }

    private async void Start()
    {
        this.mapGenerator = GetComponent<MapGenerator>();
        await this.UpdateActiveChunks();
    }

    private async void Update()
    {
        float viewerDistance = Vector3.Distance(Viewer.position, lastKnownViewerPosition);
        if (viewerDistance > ViewerMoveThresholdForUpdate)
        {
            lastKnownViewerPosition = Viewer.position;
            await UpdateActiveChunks();
        }
    }

    private async Task UpdateActiveChunks()
    {
        if (generating)
            return;
        generating = true;

        List<TerrainChunk> newChunksVisible = new List<TerrainChunk>();
        Vector2 currentChunkPos = GetClosestChunk(Viewer.position);

        List<Vector2> chunkPositions = new List<Vector2>();
        for (int x = -((int)ChunksToLoad.x); x <= (int)ChunksToLoad.x; x++)
        {
            for (int y = -((int)ChunksToLoad.y); y <= (int)ChunksToLoad.y; y++)
            {
                chunkPositions.Add(new Vector2(currentChunkPos.x + x, currentChunkPos.y + y));
            }
        }

        // Sort by distance to the player.
        chunkPositions.Sort((a, b) =>
            Vector2.Distance(a, currentChunkPos).CompareTo(Vector2.Distance(b, currentChunkPos))
        );

        foreach (var chunkPos in chunkPositions)
        {
            int renderDetail = GetRenderDetail(currentChunkPos, chunkPos);

            if (!terrainChunks.ContainsKey(chunkPos))
            {
                TerrainChunk newChunk = mapGenerator.GenerateChunkInstance();
                await newChunk.Generate(this.mapGenerator, chunkPos, mapGenerator.MapChunkSize, renderDetail);

                if (!terrainChunks.ContainsKey(chunkPos))
                    terrainChunks.Add(chunkPos, newChunk);
            }

            if (terrainChunks[chunkPos].renderDetail != renderDetail)
            {
                terrainChunks[chunkPos].SetRenderDetail(renderDetail);
                await terrainChunks[chunkPos].UpdateTerrainAsync();
            }

            terrainChunks[chunkPos].SetVisible(true);
            newChunksVisible.Add(terrainChunks[chunkPos]);
        }

        foreach (TerrainChunk chunk in activeChunks.Except(newChunksVisible))
        {
            chunk.SetVisible(false);
        }

        activeChunks = newChunksVisible;
        generating = false;
    }

    /// <summary>
    /// Returns the closest terrain chunk to be decided as the assumed primary chunk.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    private Vector2 GetClosestChunk(Vector3 pos)
    {
        return new Vector2(
            Mathf.Round(pos.x / mapGenerator.MapChunkSize),
            Mathf.Round(pos.z / mapGenerator.MapChunkSize));
    }

    private int GetRenderDetail(Vector2 followerChunk, Vector2 currentChunk)
    {
        float distance = Mathf.Max(Mathf.Abs(followerChunk.x - currentChunk.x), Mathf.Abs(followerChunk.y - currentChunk.y));

        if (distance == 0)  // Player's current chunk
        {
            return 1;
        }
        else if (distance == 1)  // First radius layer
        {
            return 2;
        }
        else if (distance == 2)  // Second radius layer
        {
            return 6;
        }
        else
        {
            return 12;
        }
    }
}