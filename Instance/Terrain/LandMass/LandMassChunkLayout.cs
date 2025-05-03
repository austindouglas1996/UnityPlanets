using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LandMassChunkLayout : IChunkLayout
{
    private LandMassChunkConfiguration Configuration;
    private BaseMarchingCubeGenerator Generator;
    private HashSet<Vector3Int> IgnoreChunks = new HashSet<Vector3Int>();

    public LandMassChunkLayout(LandMassChunkGenerator generator, LandMassChunkConfiguration configuration)
    {
        Configuration = configuration;
        Generator = generator.CreateMapGenerator(configuration);
    }

    public HashSet<Vector3Int> GetActiveChunkCoordinates(Vector3 followerPosition)
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

                    // Seen chunks, we know it is already empty.
                    if (IgnoreChunks.Contains(offset))
                        continue;

                    // Does this chunk contain data to be rendered? 
                    if (!Generator.ShouldGenerateChunk(offset, Configuration.ChunkSize))
                    {
                        IgnoreChunks.Add(offset);
                        continue;
                    }

                    chunksToLoad.Add(offset);
                }
            }
        }

        // Sort by distance.
        chunksToLoad.Sort((a, b) =>
            Vector3.Distance(a, followerChunkPos).CompareTo(Vector3.Distance(b, followerChunkPos))
        );

        return chunksToLoad.ToHashSet();
    }

    public int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate)
    {
        return 1;
    }
}