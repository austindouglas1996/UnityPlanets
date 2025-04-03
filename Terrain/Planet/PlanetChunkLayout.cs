using System.Collections.Generic;
using UnityEngine;

public class PlanetChunkLayout : IChunkLayout
{
    private Planet Planet;
    private PlanetChunkConfiguration Configuration;

    public PlanetChunkLayout(Planet planet, PlanetChunkConfiguration configuration)
    {
        this.Planet = planet;
        this.Configuration = configuration;
    }

    public List<Vector3Int> GetActiveChunkCoordinates(Vector3 followerPosition)
    {
        int chunkSize = Configuration.ChunkSize;
        int maxChunkOffset = Mathf.CeilToInt(Configuration.MaxLoadRadius / chunkSize);

        // Convert world position to chunk coordinate
        Vector3Int centerChunkCoord = WorldToChunkCoord(followerPosition);

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
                    if (Vector3.Distance(chunkCenter, followerPosition) <= Configuration.MaxLoadRadius &&
                        Vector3.Distance(chunkCenter, Planet.Center) <= Planet.PlanetRadius + 120)
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

    public int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate)
    {
        return 1;
    }

    private Vector3Int WorldToChunkCoord(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / Configuration.ChunkSize),
            Mathf.FloorToInt(worldPos.y / Configuration.ChunkSize),
            Mathf.FloorToInt(worldPos.z / Configuration.ChunkSize)
        );
    }
}