using System.Threading;
using UnityEngine;

public abstract class GenericChunkControllerFactory : IChunkControllerFactory
{
    private ChunkPool chunkPool;
    private IChunkServices chunkServices;
    private Transform parent;

    public GenericChunkControllerFactory(int preloadChunks, IChunkServices services, Transform parent)
    {
        this.chunkServices = services;
        this.parent = parent;

        GameObject newGO = new GameObject();
        newGO.AddComponent<ChunkController>();

        chunkPool = new ChunkPool(newGO, 600, parent);
    }

    protected ChunkPool Pool
    {
        get { return chunkPool; }
        set { chunkPool = value; }
    }

    public virtual ChunkController CreateChunkController(Vector3Int coordinates, int lodIndex, CancellationToken cancellationToken)
    {
        ChunkController newChunk = chunkPool.GetController();
        newChunk.transform.position = this.chunkServices.Layout.ToWorld(coordinates, lodIndex);
        newChunk.transform.parent = parent;

        // Give 0 for the LOD as other LODS will not be rendered as objects.
        newChunk.Initialize(coordinates, cancellationToken);

        return newChunk;
    }

    public void Release(ChunkController chunkController)
    {
        this.chunkPool.Release(chunkController);
    }
}