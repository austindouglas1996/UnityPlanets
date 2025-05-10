using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LandMassChunkLayout : GenericChunkLayout
{
    private BaseMarchingCubeGenerator Generator;

    public LandMassChunkLayout(LandMassChunkGenerator generator, LandMassChunkConfiguration configuration)
        : base(configuration)
    {
        Configuration = configuration;
        Generator = generator.CreateMapGenerator(configuration);
    }

    protected override ChunkResponse GetChunkResponse(Vector3Int followerCoordinates, Vector3Int coordinates)
    {
        if (!Generator.ShouldGenerateChunk(coordinates, Configuration.ChunkSize))
            return ChunkResponse.Air;

        return ChunkResponse.Surface;
    }
}