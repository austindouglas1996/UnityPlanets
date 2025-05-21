using System.Threading;
using UnityEngine;

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    public PlanetChunkControllerFactory(Planet planet, int preloadChunks, IChunkServices services, Transform parent)
        : base(preloadChunks, services, parent)
    {
    }
}
