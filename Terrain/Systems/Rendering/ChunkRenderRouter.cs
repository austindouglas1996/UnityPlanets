using System;
using UnityEngine;

/// <summary>
/// Thin “lane switch” over two <see cref="ChunkRenderBucketCollection"/>s:
/// one for LOD0, one for everything else. I hand it <see cref="ChunkKey"/>s and
/// it routes them to the right pool; I call <see cref="Update"/> / <see cref="Draw(Material)"/> once.
/// No Unity-specific bits here except the draw material.
/// </summary>
/// <remarks>
/// - Not thread-safe; call from the main Unity thread.
/// - LOD0 physics/collider hookups should live in your MonoBehaviour; this just routes keys.
/// </remarks>
public class ChunkRenderRouter : IDisposable
{
    private ChunkRenderBucketCollection lod0;
    private ChunkRenderBucketCollection main;
    private ChunkRenderBucketCollection edge;
    private IChunkGenerator chunkGenerator;

    /// <summary>
    /// Build two lanes with their own capacities/thresholds.
    /// </summary>
    /// <param name="chunkGenerator">Shared generator used by both lanes.</param>
    /// <param name="mainCap">Max items per non-LOD0 bucket (default: 128).</param>
    /// <param name="mainThres">Removals before a non-LOD0 bucket regenerates.</param>
    /// <param name="lod0Cap">Max items per LOD0 bucket (keep this small: 24–32 is nice).</param>
    /// <param name="lod0Thres">Removals before an LOD0 bucket regenerates.</param>
    public ChunkRenderRouter(IChunkGenerator chunkGenerator, int mainCap, int mainThres, int lod0Cap, int lod0Thres)
    {
        this.chunkGenerator = chunkGenerator;
        lod0 = new ChunkRenderBucketCollection(chunkGenerator, false, false, lod0Cap, lod0Thres);
        main = new ChunkRenderBucketCollection(chunkGenerator, false, false, mainCap, mainThres);
        edge = new ChunkRenderBucketCollection(chunkGenerator, false, true, mainCap, mainThres);
    }

    /// <summary>
    /// Route a key into the right lane by its <see cref="ChunkKey.LODIndex"/>.
    /// Collection handles dedupe; this is a simple forwarder.
    /// </summary>
    public void Add(ChunkGenerationJob job)
    {
        if (job.Key.LODIndex == 0)
            lod0.Add(job.Key);
        else if (job.IsEdge)
            edge.Add(job.Key);
        else
            main.Add(job.Key);
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
            return main.Remove(key) || edge.Remove(key);
    }

    /// <summary>
    /// Remove all elements.
    /// </summary>
    public void Clear()
    {
        lod0.Clear(); main.Clear(); edge.Clear();
    }

    /// <summary>
    /// Tick both lanes. Buckets handle their own debounce/regeneration internally.
    /// </summary>
    public void Update()
    {
        lod0.Update();
        main.Update();
        edge.Update();
    }

    /// <summary>
    /// Draw both lanes. Caller should have already done <c>mat.SetPass(0)</c> once this frame.
    /// Buckets will bind their buffers and issue <c>DrawProceduralIndirect</c>.
    /// </summary>
    public void Draw()
    {
        lod0.Draw(this.chunkGenerator.GetMaterial);
        main.Draw(this.chunkGenerator.GetMaterial);
        edge.Draw(this.chunkGenerator.GetMaterial);
    }

    /// <summary>
    /// Dispose both lanes and release GPU memory.
    /// Safe to call during teardown.
    /// </summary>
    public void Dispose()
    {
        lod0.Dispose();
        main.Dispose();
        edge.Dispose();
    }

    /// <summary>
    /// Tell both lanes to regenerate. 
    /// <paramref name="force"/> skips their small debounce (use sparingly).
    /// </summary>
    public void MarkAsDirty(bool force)
    {
        lod0.MarkAsDirty(force);
        main.MarkAsDirty(force);
        edge.MarkAsDirty(force);
    }
}
