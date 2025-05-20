using System.Threading;
using UnityEngine;

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public LandMassChunkControllerFactory(int preloadChunks, ChunkManager manager) 
        : base(preloadChunks, manager)
    {

    }
}
