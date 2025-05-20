using System.Threading;
using UnityEngine;

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    public PlanetChunkControllerFactory(Planet planet, int preloadChunks, ChunkManager manager)
        : base(preloadChunks, manager)
    {
    }
}
