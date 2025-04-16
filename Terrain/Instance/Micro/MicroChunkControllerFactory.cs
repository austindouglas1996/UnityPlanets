using UnityEngine;

public class MicroChunkControllerFactory : GenericChunkControllerFactory
{

    public override ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent)
    {
        ChunkController controller = base.CreateChunkController(coordinates, config, parent);

        return controller;
    }
}