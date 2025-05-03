using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PlanetChunkLayout : GenericChunkLayout
{
    private Planet Planet;

    public PlanetChunkLayout(Planet planet, PlanetChunkConfiguration configuration)
        : base(configuration)
    {
        this.Planet = planet;
    }

    public new PlanetChunkConfiguration Configuration
    {
        get { return (PlanetChunkConfiguration)base.Configuration; }
    }

    public override int ChunkRenderDistanceInChunks { get; protected set; }
    protected override ChunkResponse GetChunkResponse(Vector3Int followerCoordinates, Vector3Int coordinates)
    {
        // Get the chunk's world-space center
        Vector3 chunkCenter = (Vector3)coordinates * Configuration.ChunkSize + Vector3.one * (Configuration.ChunkSize / 2f);

        // Check both the follower load radius and the planet boundary.
        if (Vector3.Distance(chunkCenter, followerCoordinates) <= Configuration.MaxLoadRadius &&
            Vector3.Distance(chunkCenter, Planet.Center) <= Planet.PlanetRadius + Configuration.SurfaceBuffer)
        {
            return ChunkResponse.Surface;
        }

        return ChunkResponse.Air;
    }
}