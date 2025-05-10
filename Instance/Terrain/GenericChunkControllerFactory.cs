using System.Threading;
using UnityEngine;

public abstract class GenericChunkControllerFactory : IChunkControllerFactory
{
    private ChunkPool chunkPool;
    private ChunkManager chunkManager;

    public GenericChunkControllerFactory(int preloadChunks, ChunkManager manager)
    {
        this.chunkManager = manager;

        GameObject newGO = new GameObject();
        newGO.AddComponent<ChunkController>();

        chunkPool = new ChunkPool(newGO, 200, manager.transform);
    }

    protected ChunkPool Pool
    {
        get { return chunkPool; }
        set { chunkPool = value; }
    }

    protected ChunkManager ChunkManager
    {
        get { return chunkManager; }
    }

    public virtual ChunkController CreateChunkController(Vector3Int coordinates, CancellationToken cancellationToken)
    {
        Vector3 pos = new Vector3(
            coordinates.x * chunkManager.Configuration.ChunkSize,
            coordinates.y * chunkManager.Configuration.ChunkSize,
            coordinates.z * chunkManager.Configuration.ChunkSize);

        ChunkController newChunk = chunkPool.GetController();
        newChunk.transform.position = pos;
        newChunk.transform.parent = chunkManager.transform;

        // Give 0 for the LOD as other LODS will not be rendered as objects.
        newChunk.Initialize(this.chunkManager, coordinates, 0, cancellationToken);

        return newChunk;
    }

    public void Release(ChunkController chunkController)
    {
        this.chunkPool.Release(chunkController);
    }
}