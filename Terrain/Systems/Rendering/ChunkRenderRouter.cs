using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Thin “lane switch” over two <see cref="ChunkRenderBucketCollection"/>s:
/// one for LOD0, one for everything else. I hand it <see cref="ChunkKey"/>s and
/// it routes them to the right pool; I call <see cref="Update"/> / <see cref="Draw(Material)"/> once.
/// No Unity-specific bits here except the draw material.
/// </summary>
/// <remarks>
/// - Not thread-safe; call from the main Unity thread.
/// - LOD0 physics/collider hookups lives in the MonoBehaviour; this just routes keys.
/// </remarks>
public class ChunkRenderRouter : IDisposable
{
    /// <summary>
    /// The offsets for each sector we will use for culling.
    /// </summary>
    private static readonly Vector3[] SectorDirs = new[]
    {
        new Vector3( 1, 0,  0), // East
        new Vector3( 1, 0,  1), // NE
        new Vector3( 0, 0,  1), // North
        new Vector3(-1, 0,  1), // NW
        new Vector3(-1, 0,  0), // West
        new Vector3(-1, 0, -1), // SW
        new Vector3( 0, 0, -1), // South
        new Vector3( 1, 0, -1)  // SE
    };

    /// <summary>
    /// Represents a simple entry for handling large amounts of buckets
    /// with simple culling.
    /// </summary>
    private class BucketCollectionEntry
    {
        public BucketCollectionEntry(Vector3 regionCode, ChunkRenderBucketCollection coll)
        {
            this.Region = regionCode;
            this.Collection = coll;
        }

        // NOTE: Region must be normalized.
        public Vector3 Region;
        public ChunkRenderBucketCollection Collection;
    }

    /// <summary>
    /// LOD0 is given a special collection that is different from the others.
    /// This is important because the player will always (hopefully?) be near
    /// the LOD0 chunks and rendering them is important.
    /// </summary>
    private ChunkRenderBucketCollection lod0;

    /// <summary>
    /// A collection of main buckets used throughout generation.
    /// </summary>
    private List<BucketCollectionEntry> mainBuckets = new();

    private IChunkServices chunkServices;
    private IChunkGenerator chunkGenerator;
    private CommandBuffer commandBuffer;

    /// <summary>
    /// Build two lanes with their own capacities/thresholds.
    /// </summary>
    /// <param name="chunkGenerator">Shared generator used by both lanes.</param>
    /// <param name="mainCap">Max items per non-LOD0 bucket (default: 128).</param>
    /// <param name="mainThres">Removals before a non-LOD0 bucket regenerates.</param>
    /// <param name="lod0Cap">Max items per LOD0 bucket (keep this small: 24–32 is nice).</param>
    /// <param name="lod0Thres">Removals before an LOD0 bucket regenerates.</param>
    public ChunkRenderRouter(IChunkServices services, IChunkGenerator chunkGenerator, int mainCap, int lod0Cap)
    {
        this.chunkServices = services;
        this.chunkGenerator = chunkGenerator;
        lod0 = new ChunkRenderBucketCollection(chunkGenerator, true, lod0Cap);

        foreach (var sector in SectorDirs)
        {
            var bucket = new ChunkRenderBucketCollection(chunkGenerator, false, mainCap);
            this.mainBuckets.Add(new BucketCollectionEntry(sector.normalized, bucket));
        }

        this.commandBuffer = new CommandBuffer();
        this.commandBuffer.name = "ChunkTerrainInDirect";
    }

    /// <summary>
    /// Route a key into the right lane by its <see cref="ChunkKey.LODIndex"/>.
    /// Collection handles dedupe; this is a simple forwarder.
    /// </summary>
    public void Add(ChunkGenerationJob job)
    {
        if (job.Key.LODIndex == 0)
            lod0.Add(job.Key);
        else
        {
            mainBuckets[GetRegionIndex(job.Key.Coordinates)].Collection.Add(job.Key);
        }
    }

    /// <summary>
    /// Remove a key from whichever lane owns it.
    /// Returns true if the key existed and was removed.
    /// </summary>
    public bool Remove(ChunkKey key)
    {
        if (key.LODIndex == 0)
            return lod0.Remove(key);
        else
        {
            mainBuckets[GetRegionIndex(key.Coordinates)].Collection.Remove(key);
        }

        return false;
    }

    /// <summary>
    /// Remove all elements.
    /// </summary>
    public void Clear()
    {
        lod0.Clear();

        foreach (var bucket in mainBuckets)
        {
            bucket.Collection.Clear();
        }
    }

    /// <summary>
    /// Tick both lanes. Buckets handle their own debounce/regeneration internally.
    /// </summary>
    public void Update()
    {
        lod0.Update();

        foreach (var bucket in mainBuckets)
        {
            bucket.Collection.Update();
        }
    }

    /// <summary>
    /// Draw both lanes. Caller should have already done <c>mat.SetPass(0)</c> once this frame.
    /// Buckets will bind their buffers and issue <c>DrawProceduralIndirect</c>.
    /// </summary>
    public void Draw()
    {
        this.commandBuffer.Clear();

        lod0.Draw(this.commandBuffer, this.chunkGenerator.GetMaterial);

        // We use frustum culling so buckets (Sectors) that are not visible on the
        // camera does not queue to be rendered. This is a really cheap way of doing
        // this and really efficent at what it does. 
        //
        // Maybe later we should extract this sector system to be used elsehwere hmm.
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        Vector3 camForward = Camera.main.transform.forward.normalized;

        foreach (var bucket in mainBuckets)
        {
            // NOTE: Region must be normalized.
            float dot = Vector3.Dot(camForward, bucket.Region);
            bucket.Collection.Draw(this.commandBuffer, this.chunkGenerator.GetMaterial);
        }

        Graphics.ExecuteCommandBuffer(this.commandBuffer);
    }

    /// <summary>
    /// Dispose both lanes and release GPU memory.
    /// Safe to call during teardown.
    /// </summary>
    public void Dispose()
    {
        lod0.Dispose();

        foreach (var bucket in mainBuckets)
        {
            bucket.Collection.Clear();
        }

        this.commandBuffer.Dispose();
    }

    /// <summary>
    /// Tell both lanes to regenerate. 
    /// <paramref name="force"/> skips their small debounce (use sparingly).
    /// </summary>
    public void MarkAsDirty(bool force)
    {
        lod0.MarkAsDirty(force);

        foreach (var bucket in mainBuckets)
        {
            bucket.Collection.MarkAsDirty(force);
        }
    }

    /// <summary>
    /// Returns the region index of a given chunk coordinate so it goes into the correct bucket.
    /// </summary>
    /// <param name="coord"></param>
    /// <returns></returns>
    private static int GetRegionIndex(Vector3Int coord)
    {
        // Flatten to XZ plane
        Vector2 pos = new Vector2(coord.x, coord.z).normalized;

        // atan2 gives angle in radians
        float angle = Mathf.Atan2(pos.y, pos.x);
        if (angle < 0) angle += 2 * Mathf.PI;

        // 8 slices of 45 each
        int sector = Mathf.FloorToInt(angle / (Mathf.PI / 4f));

        return sector;
    }
}
