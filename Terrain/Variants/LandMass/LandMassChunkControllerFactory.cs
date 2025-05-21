using System.Threading;
using UnityEngine;

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public LandMassChunkControllerFactory(int preloadChunks, IChunkServices services, Transform parent) 
        : base(preloadChunks, services, parent)
    {

    }
}
