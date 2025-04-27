using System.Collections.Generic;
using UnityEngine;

public class LandMassChunkLayout : IChunkLayout
{
    private LandMass land;
    private LandMassChunkConfiguration Configuration;

    public LandMassChunkLayout(LandMass land, LandMassChunkConfiguration configuration)
    {
        this.land = land;
        Configuration = configuration;
    }

    public List<Vector3Int> GetActiveChunkCoordinates(Vector3 followerPosition)
    {
        int chunkSize = this.Configuration.ChunkSize;
        int maxChunkOffset = this.Configuration.ChunkViewDistance;

        Vector3Int followerChunkPos = new Vector3Int(
            Mathf.FloorToInt(followerPosition.x / chunkSize),
            Mathf.FloorToInt(followerPosition.y / chunkSize),
            Mathf.FloorToInt(followerPosition.z / chunkSize));

        List<Vector3Int> chunksToLoad = new();

        for (int x = -maxChunkOffset; x <= maxChunkOffset; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                for (int z = -maxChunkOffset; z <= maxChunkOffset; z++)
                {
                    Vector3Int offset = followerChunkPos + new Vector3Int(x, y, z);
                    chunksToLoad.Add(offset);
                }
            }
        }

        // Sort by distance.
        chunksToLoad.Sort((a, b) =>
            Vector3.Distance(a, b).CompareTo(Vector3.Distance(b, a))
        );


        return chunksToLoad;
    }

    public int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate)
    {
        return 1;
    }
}