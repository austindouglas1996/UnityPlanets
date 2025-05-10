using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A unique way to handle memory management with chunk objects. This will pool <see cref="ChunkController"/> objects to help
/// with sudden lag spikes when creating chunks throughout the world.
/// </summary>
public class ChunkPool
{
    /// <summary>
    /// A list of available chunks.
    /// </summary>
    private Stack<ChunkController> pool = new();

    /// <summary>
    /// Default game object to be used for creation.
    /// </summary>
    private GameObject chunkPrefab;

    /// <summary>
    /// The game object that contains a <see cref="ChunkManager"/> object.
    /// </summary>
    private Transform parent;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkPool"/> class.
    /// </summary>
    /// <param name="chunkPrefab"></param>
    /// <param name="preload"></param>
    /// <param name="parent"></param>
    /// <exception cref="System.ArgumentNullException"></exception>
    public ChunkPool(GameObject chunkPrefab, int preload, Transform parent)
    {
        if (chunkPrefab == null)
            throw new System.ArgumentNullException(nameof(chunkPrefab));
        if (parent == null)
            throw new System.ArgumentNullException(nameof(parent));

        this.chunkPrefab = chunkPrefab;
        this.parent = parent;

        for (int i = 0; i < preload; i++)
        {
            pool.Push(CreateController());
        }
    }

    /// <summary>
    /// Retrieves an available chunk from the pool, or creates one.
    /// </summary>
    /// <returns></returns>
    public ChunkController GetController()
    {
        if (pool.Count > 0)
        {
            var chunk = pool.Pop();
            chunk.gameObject.SetActive(true);
            return chunk;
        }

        return CreateController(true); 
    }

    /// <summary>
    /// Releases a chunk so it may be added back to the pool of available objects.
    /// </summary>
    /// <param name="controller"></param>
    public void Release(ChunkController controller)
    {
        controller.gameObject.SetActive(false);
        controller.transform.SetParent(parent);
        pool.Push(controller);
    }

    /// <summary>
    /// Create a new <see cref="ChunkController"/> object based on the prefab given.
    /// </summary>
    /// <param name="isActive"></param>
    /// <returns></returns>
    private ChunkController CreateController(bool isActive = false)
    {
        var newChunk = GameObject.Instantiate(chunkPrefab);
        newChunk.name = $"PooledChunk";
        newChunk.SetActive(isActive);
        newChunk.transform.SetParent(parent);

        return newChunk.GetComponent<ChunkController>();
    }
}