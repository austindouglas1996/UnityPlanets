using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pools <see cref="ChunkController"/> objects to reduce runtime lag spikes when generating lots of chunks.
/// Helps smooth out gameplay by reusing inactive GameObjects.
/// </summary>
public class ChunkPool
{
    /// <summary>
    /// Unity’s internal object pool.
    /// </summary>
    private ObjectPool<ChunkController> pool;

    /// <summary>
    /// The prefab used to create new chunk instances.
    /// </summary>
    private GameObject chunkPrefab;

    /// <summary>
    /// Where chunk instances will be parented in the hierarchy.
    /// </summary>
    private Transform parent;

    /// <summary>
    /// Initializes a new <see cref="ChunkPool"/> using Unity’s built-in pool system.
    /// </summary>
    /// <param name="chunkPrefab">The prefab to use when creating new chunks.</param>
    /// <param name="preload">How many chunks to create at the start.</param>
    /// <param name="parent">The transform to parent pooled chunks under.</param>
    public ChunkPool(GameObject chunkPrefab, int preload, Transform parent)
    {
        if (chunkPrefab == null)
            throw new System.ArgumentNullException(nameof(chunkPrefab));
        if (parent == null)
            throw new System.ArgumentNullException(nameof(parent));

        this.chunkPrefab = chunkPrefab;
        this.parent = parent;

        // Create the Unity object pool
        this.pool = new ObjectPool<ChunkController>(
            createFunc: CreateController,
            actionOnGet: chunk => {
                chunk.gameObject.SetActive(true);
            },
            actionOnRelease: chunk => {
                chunk.ResetController();
                chunk.name = "ReleasedToPool";
                chunk.gameObject.SetActive(false);
                chunk.transform.SetParent(parent);
            },
            actionOnDestroy: chunk => GameObject.Destroy(chunk.gameObject),
            collectionCheck: false,
            defaultCapacity: preload,
            maxSize: 10000
        );

        // Preload instances
        for (int i = 0; i < preload; i++)
        {
            var controller = pool.Get();
            pool.Release(controller);
        }
    }

    /// <summary>
    /// Retrieve a chunk from the pool or create one if none are available.
    /// </summary>
    public ChunkController GetController() => pool.Get();

    /// <summary>
    /// Return a chunk to the pool.
    /// </summary>
    public void Release(ChunkController controller) => pool.Release(controller);

    /// <summary>
    /// Actually instantiates a new chunk from the prefab.
    /// </summary>
    private ChunkController CreateController()
    {
        var newChunk = GameObject.Instantiate(chunkPrefab);
        newChunk.isStatic = true;
        newChunk.name = "PooledChunk";
        newChunk.transform.SetParent(parent);
        return newChunk.GetComponent<ChunkController>();
    }
}
