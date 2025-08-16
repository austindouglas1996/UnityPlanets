using System;
using System.Collections.Generic;
using UnityEngine;
using VHierarchy.Libs;

/// <summary>
/// A collection container for <see cref="ChunkRenderBucket"/> that can grow as needed
/// while keeping itself tidy. Think of it like an expandable <see cref="List{T}"/> of buckets
/// where I don't care which bucket a key lands in—just that it lands somewhere fast.
/// </summary>
public class ChunkRenderBucketCollection : IDisposable
{
    private List<ChunkRenderBucket> buckets = new();

    /// <summary>
    /// Buckets that were full but now have room again. I try to refill these first to keep buckets packed.
    /// </summary>
    private List<ChunkRenderBucket> bucketsWithSpace = new();

    /// <summary>
    /// Who owns a key right now? (instant remove, no scans)
    /// </summary>
    private readonly Dictionary<ChunkKey, ChunkRenderBucket> keys = new();
    private readonly Dictionary<ChunkRenderBucket, GameObject> colliders = new();

    private int capacity = 128;
    private int rebuildThreshold = 1;
    private bool isLod0 = false;

    private IChunkGenerator chunkGenerator;

    /// <summary>
    /// Initalize a new instance of <see cref="ChunkRenderBucketCollection"/> with available capacity and rebuild thresholds.
    /// </summary>
    /// <param name="chunkGenerator">Shared generator used by all buckets.</param>
    /// <param name="capacity">Max items per bucket (default 128).</param>
    /// <param name="rebuiltThreshold">Removals per bucket before it regenerates.</param>
    public ChunkRenderBucketCollection(IChunkGenerator chunkGenerator, bool isLod0 = false, int capacity = 128, int rebuiltThreshold = 64)
    {
        this.chunkGenerator = chunkGenerator;
        this.capacity = capacity;
        this.rebuildThreshold = rebuiltThreshold;
        this.isLod0 = isLod0;
    }

    /// <summary>
    /// Add a key to the collection. No-op if it already exists.
    /// Prefers refilling a previously-full bucket before touching the tail or creating a new one.
    /// </summary>
    /// <param name="key"></param>
    public void Add(ChunkKey key)
    {
        if (keys.ContainsKey(key)) return;

        var bucket = GetOrCreateTailBucket();

        if (bucket.TryAdd(key))
        {
            keys.Add(key, bucket);

            if (bucket.IsFull && bucketsWithSpace.Contains(bucket))
                bucketsWithSpace.Remove(bucket);
        }
        else
        {
            Debug.LogWarning("Error: Failed to add key to bucket.");
        }
    }

    /// <summary>
    /// Try to remove an existing key from our bucket collection.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool Remove(ChunkKey key)
    {
        if (!keys.ContainsKey(key)) return false;

        var bucket = keys[key];

        if (bucket.TryRemove(key))
        {
            this.keys.Remove(key);

            if (!bucketsWithSpace.Contains(bucket))
                bucketsWithSpace.Add(bucket);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Update the bucket collections.
    /// </summary>
    public void Update()
    {
        for (int i = 0; i < buckets.Count; i++)
            buckets[i].Update();
    }

    /// <summary>
    /// Draw the buckets render data.
    /// </summary>
    /// <param name="material"></param>
    public void Draw(Material material)
    {
        for (int i = 0; i < buckets.Count; i++)
            buckets[i].Draw(material);
    }

    /// <summary>
    /// Dispose of each bucket.
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < buckets.Count; i++)
            buckets[i].Dispose();

        buckets = null;
    }

    /// <summary>
    /// Mark each bucket as dirty so they re-generate.
    /// </summary>
    /// <param name="force"></param>
    public void MarkAsDirty(bool force)
    {
        for (int i = 0; i < buckets.Count; i++)
            buckets[i].MarkAsDirty(force);
    }

    /// <summary>
    /// Pick where the next add goes:
    /// 1) a bucket that freed up (bucketsWithSpace[0]),
    /// 2) the current tail if it isn't full,
    /// 3) or create a new bucket.
    /// </summary>
    /// <returns></returns>
    private ChunkRenderBucket GetOrCreateTailBucket()
    {
        if (bucketsWithSpace.Count != 0)
            return bucketsWithSpace[0];

        if (buckets.Count == 0)
            return CreateBucket();

        var tail = buckets[^1];
        if (!tail.IsFull) return tail;

        return CreateBucket();
    }

    /// <summary>
    /// Create a new bucket.
    /// </summary>
    /// <returns></returns>
    private ChunkRenderBucket CreateBucket()
    {
        var newColl = new ChunkRenderBucket(capacity, rebuildThreshold, this.chunkGenerator);
        buckets.Add(newColl);

        if (isLod0)
        {
            newColl.OnGenerate += NewColl_OnGenerate;
        }

        return newColl;
    }

    /// <summary>
    /// Generate a <see cref="GameObject"/> for collision on the terrain.
    /// </summary>
    /// <param name="sender">The bucket with the <see cref="ChunkRenderBucket"/></param>
    /// <param name="e"></param>
    /// <remarks>This is not thread safe. Must be called from main thread.</remarks>
    private void NewColl_OnGenerate(object sender, EventArgs e)
    {
        ChunkRenderBucket bucket = (ChunkRenderBucket)sender;
        ChunkRenderBatch.ReadTrianglesAsync(bucket.RenderData, (ChunkTriangleData[] tri) =>
        {
            var mesh = TriangleMeshBuilder.BuildMesh(tri);
            var newGo = TriangleMeshBuilder.CreateGOMeshWithCollider(mesh);
            GameObject oldGo = null;

            if (colliders.ContainsKey(bucket))
                oldGo = colliders[bucket];

            colliders[bucket] = newGo;

            if (oldGo != null)
                oldGo.Destroy();
        });
    }
}