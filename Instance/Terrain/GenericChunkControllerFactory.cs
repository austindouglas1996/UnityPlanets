using System.Threading;
using UnityEngine;

public abstract class GenericChunkControllerFactory : IChunkControllerFactory
{
    public virtual ChunkController CreateChunkController(Vector3Int coordinates, ChunkManager manager, IChunkConfiguration config, Transform parent, CancellationToken cancellationToken)
    {
        Vector3 pos = new Vector3(
            coordinates.x * config.ChunkSize,
            coordinates.y * config.ChunkSize,
            coordinates.z * config.ChunkSize);

        GameObject newChunk = new GameObject();
        newChunk.transform.position = pos;
        newChunk.transform.parent = parent;

        ChunkController newController = newChunk.AddComponent<ChunkController>();

        return newController;
    }

    public abstract IChunkGenerator CreateGenerator();
}